using CoreKit.Identity.Common;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;
using CoreKit.Identity.Normalization;
using CoreKit.Identity.Persistence;
using CoreKit.Identity.Security;
using CoreKit.Identity.Validation;
using CoreKit.Identity.IntegrationEvents;

namespace CoreKit.Identity.Services;

/// <summary>
/// Identity user management. Authorization for user-management operations is delegated to the host
/// through <see cref="IIdentityAdminAuthorizer"/>; platform roles and membership are owned by Access.
/// Identity keeps the user record, credentials and the last-active-administrator protection.
/// </summary>
public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IdentityDbContext _db;
    private readonly IIdentityAdminAuthorizer _authorizer;
    private readonly IAdministratorGuard _administratorGuard;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _time;
    private readonly IIdentityEventPublisher _eventPublisher;

    public UserService(
        IUserRepository userRepository,
        IdentityDbContext db,
        IIdentityAdminAuthorizer authorizer,
        IAdministratorGuard administratorGuard,
        IPasswordHasher hasher,
        ICurrentUserService currentUser,
        TimeProvider time,
        IIdentityEventPublisher eventPublisher)
    {
        _userRepository = userRepository;
        _db = db;
        _authorizer = authorizer;
        _administratorGuard = administratorGuard;
        _hasher = hasher;
        _currentUser = currentUser;
        _time = time;
        _eventPublisher = eventPublisher;
    }

    public async Task<IdentityResult<PagedResult<UserDto>>> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        if (!await _authorizer.IsAllowedAsync(IdentityAdminOperation.ReadUsers, ct))
            return IdentityErrors.AccessDenied("You cannot view users.");

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _userRepository.ListAsync(query.Search, query.IsActive, page, pageSize, ct);

        return Common.IdentityResult.Success(new PagedResult<UserDto>
        {
            Items = items.Select(u => IdentityMapper.ToDto(u)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<IdentityResult<UserDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (!await _authorizer.IsAllowedAsync(IdentityAdminOperation.ReadUsers, ct))
            return IdentityErrors.AccessDenied("You cannot view users.");

        return await LoadDtoAsync(id, ct);
    }

    private async Task<IdentityResult<UserDto>> LoadDtoAsync(Guid id, CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(id, ct);

        if (user is null)
            return IdentityErrors.NotFound("User");

        return Common.IdentityResult.Success(IdentityMapper.ToDto(user));
    }

    public async Task<IdentityResult<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (!await _authorizer.IsAllowedAsync(IdentityAdminOperation.CreateUsers, ct))
            return IdentityErrors.AccessDenied("You cannot create users.");

        var email = request.Email?.Trim() ?? string.Empty;

        if (!EmailValidator.IsValid(email))
            return IdentityErrors.Validation("A valid email address is required.");

        var mobile = request.Mobile?.Trim() ?? string.Empty;

        if (mobile.Length == 0)
            return IdentityErrors.Validation("A mobile number is required.");

        var normalizedMobile = MobileValidator.Normalize(mobile);

        if (normalizedMobile is null)
            return IdentityErrors.Validation("A valid mobile number is required.");

        var passwordError = PasswordPolicy.Validate(request.Password, email);

        if (passwordError is not null)
            return IdentityErrors.Validation(passwordError);

        var nameError = DisplayNameValidator.Validate(request.DisplayName, out var displayName);

        if (nameError is not null)
            return nameError;

        var normalizedEmail = IdentityNormalizer.NormalizeEmail(email);

        if (await _userRepository.EmailExistsAsync(normalizedEmail, ct: ct))
            return IdentityErrors.Conflict("A user with this email already exists.");

        if (await _userRepository.MobileExistsAsync(normalizedMobile, ct: ct))
            return IdentityErrors.Conflict("A user with this mobile number already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            Mobile = mobile,
            NormalizedMobile = normalizedMobile,
            PasswordHash = _hasher.Hash(request.Password),
            DisplayName = displayName,
            IsActive = request.IsActive,
            EmailConfirmed = false,
            CreatedAt = Now()
        };

        if (!await _userRepository.CreateAsync(user, ct))
            return IdentityErrors.Conflict("A user with this email or mobile number already exists.");

        return await LoadDtoAsync(user.Id, ct);
    }

    public async Task<IdentityResult<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        if (!await _authorizer.IsAllowedAsync(IdentityAdminOperation.UpdateUsers, ct))
            return IdentityErrors.AccessDenied("You cannot update users.");

        if (_currentUser.UserId is not { } actorId)
            return IdentityErrors.AccessDenied("You cannot update users.");

        var user = await _userRepository.FindForUpdateAsync(id, ct);

        if (user is null)
            return IdentityErrors.NotFound("User");

        // The email address is established at account creation and is immutable afterwards.
        if (request.Email is not null)
            return IdentityErrors.Validation("The email address cannot be changed after account creation.");

        var now = Now();

        if (request.Mobile is { } mobile && mobile != user.Mobile)
        {
            var normalizedMobile = MobileValidator.Normalize(mobile);

            if (normalizedMobile is null)
                return IdentityErrors.Validation("A valid mobile number is required.");

            if (await _userRepository.MobileExistsAsync(normalizedMobile, id, ct))
                return IdentityErrors.Conflict("A user with this mobile number already exists.");

            user.Mobile = mobile.Trim();
            user.NormalizedMobile = normalizedMobile;
        }

        if (request.DisplayName is not null)
        {
            var nameError = DisplayNameValidator.Validate(request.DisplayName, out var displayName);

            if (nameError is not null)
                return nameError;

            user.DisplayName = displayName;
        }

        if (request.IsActive is { } isActive && isActive != user.IsActive)
        {
            if (actorId == id)
                return IdentityErrors.Forbidden("You cannot deactivate your own account.");

            if (await _administratorGuard.IsLastActiveAdministratorAsync(id, ct))
                return IdentityErrors.Conflict("The last active administrator cannot be deactivated.");

            if (!isActive)
            {
                // If password change is also requested, do both atomically in a single transaction.
                // Otherwise, use the existing deactivation-only path.
                if (request.Password is not null)
                {
                    var passwordError = PasswordPolicy.Validate(request.Password, user.Email);
                    if (passwordError is not null)
                        return IdentityErrors.Validation(passwordError);

                    var combined = await _userRepository.TryDeactivateAndChangePasswordAsync(
                        id,
                        user.SecurityVersion,
                        user.PasswordHash,
                        _hasher.Hash(request.Password),
                        now,
                        ct);

                    if (combined != CredentialUpdateResult.Updated)
                        return IdentityErrors.Conflict("The account was changed by another request. Please try again.");

                    // Both operations succeeded; skip the separate password change below.
                    request.Password = null;
                }
                else
                {
                    var deactivated = await _userRepository.TryDeactivateAsync(id, user.SecurityVersion, now, ct);

                    if (deactivated != CredentialUpdateResult.Updated)
                        return IdentityErrors.Conflict("The account was changed by another request. Please try again.");
                }
            }
            else
            {
                user.IsActive = true;
            }
        }

        if (request.Password is not null)
        {
            var passwordError = PasswordPolicy.Validate(request.Password, user.Email);

            if (passwordError is not null)
                return IdentityErrors.Validation(passwordError);

            var reset = await _userRepository.TryChangePasswordAsync(
                id,
                user.PasswordHash,
                user.SecurityVersion,
                _hasher.Hash(request.Password),
                now,
                bumpSecurityVersion: true,
                revokeSessions: true,
                ct);

            if (reset != CredentialUpdateResult.Updated)
                return IdentityErrors.Conflict("The account was changed by another request. Please try again.");
        }

        user.UpdatedAt = now;

        if (!await _userRepository.UpdateAsync(user, ct))
            return IdentityErrors.Conflict("A user with this email or mobile number already exists.");

        return await LoadDtoAsync(id, ct);
    }

    public async Task<Common.IdentityResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await _authorizer.IsAllowedAsync(IdentityAdminOperation.DeleteUsers, ct))
            return IdentityErrors.AccessDenied("You cannot delete users.");

        if (_currentUser.UserId == id)
            return IdentityErrors.Forbidden("You cannot delete your own account.");

        var user = await _userRepository.FindForUpdateAsync(id, ct);

        if (user is null)
            return IdentityErrors.NotFound("User");

        if (await _administratorGuard.IsLastActiveAdministratorAsync(id, ct))
            return IdentityErrors.Conflict("The last active administrator cannot be deleted.");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        _userRepository.StageDelete(user);

        var now = Now();
        await _eventPublisher.PublishAsync(new IdentityUserDeleted(id, now), ct);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Common.IdentityResult.Success();
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}
