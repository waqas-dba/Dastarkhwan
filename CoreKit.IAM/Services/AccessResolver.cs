using CoreKit.IAM.Entities;
using CoreKit.IAM.Interfaces;

namespace CoreKit.IAM.Services;

/// <summary>
/// What the user may actually do right now: the names of their active roles and the active
/// permissions of those roles. Inactive roles and permissions grant nothing.
/// </summary>
internal sealed class AccessResolver : IAccessResolver
{
    private readonly IPermissionRepository _permissionRepository;

    public AccessResolver(IPermissionRepository permissionRepository)
        => _permissionRepository = permissionRepository;

    public async Task<UserAccess> ResolveAsync(User user, CancellationToken ct = default)
    {
        var activeRoles = user.UserRoles.Where(ur => ur.Role.IsActive).ToList();
        var roleNames = activeRoles.Select(ur => ur.Role.Name).Distinct().OrderBy(name => name).ToList();
        var roleIds = activeRoles.Select(ur => ur.RoleId).ToList();

        if (roleIds.Count == 0)
            return new UserAccess(roleNames, new List<string>());

        var permissionNames = await _permissionRepository.GetActiveNamesForRolesAsync(roleIds, ct);

        return new UserAccess(roleNames, permissionNames);
    }
}