using CoreKit.Access.Abstractions;
using CoreKit.Identity.Common;
using CoreKit.Identity.Constants;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;
using CoreKit.Identity.Normalization;
using CoreKit.Identity.Security;
using CoreKit.Identity.Validation;

namespace CoreKit.Identity.Services;

/// <summary>
/// Identity user management. Authorization and role/permission authority live in Access: Identity asks
/// Access whether the signed-in user holds an identity.* permission in the platform scope, and delegates
/// platform membership and role assignment to Access. Identity keeps the user record, credentials and the
/// last-active-administrator protection.
/// </summary>
public sealed class UserService : IUserService
{
    private static readonly AccessScope PlatformScope = AccessScope.ForPlatform(AccessScope.PlatformId);

    private readonly IUserRepository _userRepository;
    private readonly IPermissionChecker _permissions;
    private readonly IAccessService _access;
    private readonly IAccessMaintenance _maintenance;
    private readonly IAdministratorGuard _administratorGuard;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _time;

    public UserService(
        IUserRepository userRepository,
        IPermissionChecker permissions,
        IAccessService access,
        IAccessMaintenance maintenance,
        IAdministratorGuard administratorGuard,
        IPasswordHasher hasher,
        ICurrentUserService currentUser,
        TimeProvider time)
    {
        _userRepository = userRepository;
        _permissions = permissions;
        _access = access;
        _maintenance = maintenance;
        _administratorGuard = administratorGuard;
        _hasher = hasher;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<IdentityResult<PagedResult<UserDto>>> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        if (!await _permissions.HasPlatformPermissionAsync(IdentityAccessPermissionNames.UsersRead, ct))
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
        if (!await _permissions.HasPlatformPermissionAsync(IdentityAccessPermissionNames.UsersRead, ct))
            return IdentityErrors.AccessDenied("You cannot view users.");

        return await LoadDtoAsync(id, ct);
    }

    /// <summary>
    /// Builds the response after a successful write without asking for the read permission again.
    /// A caller allowed to create or update a user must not be told the write failed just because
    /// they cannot list users.
    /// </summary>
    private async Task<IdentityResult<UserDto>> LoadDtoAsync(Guid id, CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(id, ct);

        if (user is null)
            return IdentityErrors.NotFound("User");

        var access = await _access.GetAccessAsync(user.Id, PlatformScope, ct);

        return Common.IdentityResult.Success(IdentityMapper.ToDto(user, access.Roles, access.Permissions));
    }

    public async Task<IdentityResult<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (!await _permissions.HasPlatformPermissionAsync(IdentityAccessPermissionNames.UsersCreate, ct))
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

        var roleIds = (request.RoleIds ?? new List<Guid>()).Distinct().ToList();
        var roleError = await ValidateRolesAsync(roleIds, ct);

        if (roleError is not null)
            return roleError;

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

        // An inactive account is not a platform member; membership and roles are granted on activation.
        if (user.IsActive)
        {
            await _maintenance.AddPlatformMemberAsync(user.Id, ct);

            if (roleIds.Count > 0)
                await _maintenance.SetPlatformUserRolesAsync(user.Id, roleIds, ct);
        }

        return await LoadDtoAsync(user.Id, ct);
    }

    public async Task<IdentityResult<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        if (!await _permissions.HasPlatformPermissionAsync(IdentityAccessPermissionNames.UsersUpdate, ct))
            return IdentityErrors.AccessDenied("You cannot update users.");

        if (_currentUser.UserId is not { } actorId)
            return IdentityErrors.AccessDenied("You cannot update users.");

        var user = await _userRepository.FindForUpdateAsync(id, ct);

        if (user is null)
            return IdentityErrors.NotFound("User");

        var actorAccess = await _access.GetAccessAsync(actorId, PlatformScope, ct);
        var actorIsAdmin = IsAdministrator(actorAccess);

        // Only an administrator can change another administrator's credentials.
        if ((request.Password is not null || request.Email is not null) && id != actorId)
        {
            var userAccess = await _access.GetAccessAsync(id, PlatformScope, ct);

            if (IsAdministrator(userAccess) && !actorIsAdmin)
                return IdentityErrors.AccessDenied("You cannot change another administrator's credentials.");
        }

        // The email address is established at account creation and is immutable afterwards.
        // Any non-null value in the request is rejected outright — including a casing-only or
        // byte-identical "change" — before any other field is written, so an update carrying an
        // email change never partially saves.
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
            if (!isActive)
            {
                if (actorId == id)
                    return IdentityErrors.Forbidden("You cannot deactivate your own account.");

                if (await _administratorGuard.IsLastActiveAdministratorAsync(id, ct))
                    return IdentityErrors.Conflict("The last active administrator cannot be deactivated.");

                // Deactivation is a security transition: it is applied under an expected-version
                // guard, increments SecurityVersion and revokes every session in one transaction.
                // If a concurrent security-sensitive update wins the race, nothing is applied.
                var deactivated = await _userRepository.TryDeactivateAsync(id, user.SecurityVersion, now, ct);

                if (deactivated != CredentialUpdateResult.Updated)
                    return IdentityErrors.Conflict("The account was changed by another request. Please try again.");

                // Revoking platform membership ends the account's platform access and makes any stale
                // role assignments inactive (membership generation bump).
                await _maintenance.RemovePlatformMemberAsync(id, ct);
            }
            else
            {
                user.IsActive = true;
            }
        }

        // Password resets are security transitions too: a version-guarded conditional update plus
        // version increment plus session revocation, in one transaction. A tracked-entity save is
        // never used for them, so a stale snapshot cannot overwrite a newer password or state.
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

        // Reactivation grants platform membership again (idempotent).
        if (request.IsActive == true && !user.IsActive)
            await _maintenance.AddPlatformMemberAsync(id, ct);

        return await LoadDtoAsync(id, ct);
    }

    public async Task<Common.IdentityResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await _permissions.HasPlatformPermissionAsync(IdentityAccessPermissionNames.UsersDelete, ct))
            return IdentityErrors.AccessDenied("You cannot delete users.");

        if (_currentUser.UserId == id)
            return IdentityErrors.Forbidden("You cannot delete your own account.");

        var user = await _userRepository.FindForUpdateAsync(id, ct);

        if (user is null)
            return IdentityErrors.NotFound("User");

        if (await _administratorGuard.IsLastActiveAdministratorAsync(id, ct))
            return IdentityErrors.Conflict("The last active administrator cannot be deleted.");

        await _userRepository.DeleteAsync(user, ct);

        // Platform membership is revoked so the deleted user's assignments can never resurface.
        await _maintenance.RemovePlatformMemberAsync(id, ct);

        return Common.IdentityResult.Success();
    }

    public async Task<IdentityResult<UserDto>> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken ct = default)
    {
        if (!await _permissions.HasPlatformPermissionAsync(IdentityAccessPermissionNames.UsersUpdate, ct))
            return IdentityErrors.AccessDenied("You cannot update user roles.");

        if (_currentUser.UserId is not { } actorId)
            return IdentityErrors.AccessDenied("You cannot update user roles.");

        var user = await _userRepository.FindForUpdateAsync(id, ct);

        if (user is null)
            return IdentityErrors.NotFound("User");

        var wanted = (request.RoleIds ?? new List<Guid>()).Distinct().ToList();
        var roleError = await ValidateRolesAsync(wanted, ct);

        if (roleError is not null)
            return roleError;

        var platformRoles = await _access.ListRolesAsync(PlatformScope, ct);
        var adminRoleId = platformRoles.FirstOrDefault(r => r.Name == IdentityRoleNames.Administrator)?.Id;

        var wantsAdministrator = adminRoleId is { } adminId && wanted.Contains(adminId);

        if (wantsAdministrator)
        {
            var actorAccess = await _access.GetAccessAsync(actorId, PlatformScope, ct);

            if (!IsAdministrator(actorAccess))
                return IdentityErrors.AccessDenied("You cannot grant administrator privileges.");
        }

        var userAccess = await _access.GetAccessAsync(id, PlatformScope, ct);

        var losesAdministrator = IsAdministrator(userAccess) && !wantsAdministrator;

        if (losesAdministrator && await _administratorGuard.IsLastActiveAdministratorAsync(id, ct))
            return IdentityErrors.Conflict("The last active administrator cannot lose the Administrator role.");

        await _maintenance.SetPlatformUserRolesAsync(id, wanted, ct);

        return await LoadDtoAsync(id, ct);
    }

    /// <summary>
    /// Validates that every role id is a platform-scope role and that the signed-in user may grant the
    /// Administrator role. Returns null when the set is acceptable.
    /// </summary>
    private async Task<Common.IdentityError?> ValidateRolesAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0)
            return null;

        if (_currentUser.UserId is not { } actorId)
            return IdentityErrors.AccessDenied("You cannot assign roles.");

        var platformRoles = await _access.ListRolesAsync(PlatformScope, ct);
        var validIds = platformRoles.Select(r => r.Id).ToHashSet();

        if (roleIds.Any(id => !validIds.Contains(id)))
            return IdentityErrors.Validation("One or more roles do not exist.");

        var adminRoleId = platformRoles.FirstOrDefault(r => r.Name == IdentityRoleNames.Administrator)?.Id;

        if (adminRoleId is { } adminId && roleIds.Contains(adminId))
        {
            var actorAccess = await _access.GetAccessAsync(actorId, PlatformScope, ct);

            if (!IsAdministrator(actorAccess))
                return IdentityErrors.AccessDenied("You cannot grant administrator privileges.");
        }

        return null;
    }

    private static bool IsAdministrator(CoreKit.Access.Abstractions.EffectiveAccess access)
        => access.IsOwner || access.Roles.Contains(IdentityRoleNames.Administrator);

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}