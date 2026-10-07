using CoreKit.IAM.Common;
using CoreKit.IAM.Entities;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;
using CoreKit.IAM.Normalization;
using CoreKit.IAM.Security;
using CoreKit.IAM.Validation;

namespace CoreKit.IAM.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAccessResolver _accessResolver;
    private readonly IAdministratorGuard _administratorGuard;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _time;

    public UserService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAccessResolver accessResolver,
        IAdministratorGuard administratorGuard,
        IPasswordHasher hasher,
        ICurrentUserService currentUser,
        TimeProvider time)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _accessResolver = accessResolver;
        _administratorGuard = administratorGuard;
        _hasher = hasher;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<IamResult<PagedResult<UserDto>>> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _userRepository.ListAsync(query.Search, query.IsActive, page, pageSize, ct);

        return IamResult.Success(new PagedResult<UserDto>
        {
            Items = items.Select(u => IamMapper.ToDto(u)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<IamResult<UserDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdWithRolesAsync(id, ct);

        if (user is null)
            return IamErrors.NotFound("User");

        var access = await _accessResolver.ResolveAsync(user, ct);

        return IamResult.Success(IamMapper.ToDto(user, access.Permissions));
    }

    public async Task<IamResult<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim() ?? string.Empty;

        if (!EmailValidator.IsValid(email))
            return IamErrors.Validation("A valid email address is required.");

        var passwordError = PasswordPolicy.Validate(request.Password, email);

        if (passwordError is not null)
            return IamErrors.Validation(passwordError);

        var nameError = DisplayNameValidator.Validate(request.DisplayName, out var displayName);

        if (nameError is not null)
            return nameError;

        var roleIds = (request.RoleIds ?? new List<Guid>()).Distinct().ToList();
        var roles = await _roleRepository.FindByIdsAsync(roleIds, ct);

        if (roles.Count != roleIds.Count)
            return IamErrors.Validation("One or more roles do not exist.");

        var normalizedEmail = IamNormalizer.NormalizeEmail(email);

        if (await _userRepository.EmailExistsAsync(normalizedEmail, ct: ct))
            return IamErrors.Conflict("A user with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = _hasher.Hash(request.Password),
            DisplayName = displayName,
            IsActive = request.IsActive,
            EmailConfirmed = false,
            CreatedAt = Now()
        };

        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        if (!await _userRepository.CreateAsync(user, ct))
            return IamErrors.Conflict("A user with this email already exists.");

        return await GetAsync(user.Id, ct);
    }

    public async Task<IamResult<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.FindForUpdateAsync(id, ct);

        if (user is null)
            return IamErrors.NotFound("User");

        var now = Now();
        var endSessions = false;

        if (request.Email is not null)
        {
            var email = request.Email.Trim();

            if (!EmailValidator.IsValid(email))
                return IamErrors.Validation("A valid email address is required.");

            var normalized = IamNormalizer.NormalizeEmail(email);

            if (normalized != user.NormalizedEmail)
            {
                if (await _userRepository.EmailExistsAsync(normalized, id, ct))
                    return IamErrors.Conflict("A user with this email already exists.");

                user.NormalizedEmail = normalized;
                user.EmailConfirmed = false; // a new address has not been confirmed yet
            }

            user.Email = email;
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
            if (!isActive)
            {
                if (_currentUser.UserId == id)
                    return IamErrors.Forbidden("You cannot deactivate your own account.");

                if (await _administratorGuard.IsLastActiveAdministratorAsync(id, ct))
                    return IamErrors.Conflict("The last active administrator cannot be deactivated.");

                endSessions = true;
            }

            user.IsActive = isActive;
        }

        if (request.Password is not null)
        {
            var passwordError = PasswordPolicy.Validate(request.Password, user.Email);

            if (passwordError is not null)
                return IamErrors.Validation(passwordError);

            user.PasswordHash = _hasher.Hash(request.Password);
            endSessions = true;
        }

        user.UpdatedAt = now;

        if (!await _userRepository.UpdateAsync(user, ct))
            return IamErrors.Conflict("A user with this email already exists.");

        if (endSessions)
            await _refreshTokenRepository.RevokeAllForUserAsync(id, now, ct);

        return await GetAsync(id, ct);
    }

    public async Task<IamResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId == id)
            return IamErrors.Forbidden("You cannot delete your own account.");

        var user = await _userRepository.FindForUpdateAsync(id, ct);

        if (user is null)
            return IamErrors.NotFound("User");

        if (await _administratorGuard.IsLastActiveAdministratorAsync(id, ct))
            return IamErrors.Conflict("The last active administrator cannot be deleted.");

        await _userRepository.DeleteAsync(user, ct);

        return IamResult.Success();
    }

    public async Task<IamResult<UserDto>> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.FindForUpdateAsync(id, ct);

        if (user is null)
            return IamErrors.NotFound("User");

        var wanted = (request.RoleIds ?? new List<Guid>()).Distinct().ToList();
        var roles = await _roleRepository.FindByIdsAsync(wanted, ct);

        if (roles.Count != wanted.Count)
            return IamErrors.Validation("One or more roles do not exist.");

        var losesAdministrator =
            user.UserRoles.Any(ur => AdministratorRole.Is(ur.Role)) &&
            roles.All(r => !AdministratorRole.Is(r));

        if (losesAdministrator && await _administratorGuard.IsLastActiveAdministratorAsync(id, ct))
            return IamErrors.Conflict("The last active administrator cannot lose the Administrator role.");

        user.UpdatedAt = Now();

        await _userRepository.ReplaceRolesAsync(user, wanted, ct);

        return await GetAsync(id, ct);
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}