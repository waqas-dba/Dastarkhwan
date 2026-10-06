
using CoreKit.IAM.Entities;
using CoreKit.IAM.Models;
using CoreKit.IAM.Persistence;


namespace CoreKit.IAM.Services;


internal static class IamMapper
{
    /// <summary>The user must be loaded with UserRoles and each UserRole's Role.</summary>
    public static UserDto ToDto(User user, IReadOnlyCollection<string>? permissions = null) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        IsActive = user.IsActive,
        EmailConfirmed = user.EmailConfirmed,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        Roles = user.UserRoles.Select(ur => ur.Role.Name).OrderBy(name => name).ToList(),
        Permissions = permissions?.ToList() ?? new List<string>()
    };

    /// <summary>
    /// What the user may actually do right now: the names of their active roles and the active
    /// permissions of those roles. Inactive roles and permissions grant nothing.
    /// </summary>
    public static async Task<(List<string> Roles, List<string> Permissions)> ResolveAccessAsync(
        IamDbContext db, User user, CancellationToken ct)
    {
        var activeRoles = user.UserRoles.Where(ur => ur.Role.IsActive).ToList();
        var roleNames = activeRoles.Select(ur => ur.Role.Name).Distinct().OrderBy(name => name).ToList();
        var roleIds = activeRoles.Select(ur => ur.RoleId).ToList();

        if (roleIds.Count == 0)
            return (roleNames, new List<string>());

        var permissionNames = await db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId) && rp.Permission.IsActive)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(ct);

        return (roleNames, permissionNames);
    }
}