using CoreKit.IAM.Entities;
using CoreKit.IAM.Models;

namespace CoreKit.IAM.Interfaces;

public interface IRoleRepository
{
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken ct = default);

    Task<RoleDto?> GetDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tracked.</summary>
    Task<Role?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tracked, with its permission links.</summary>
    Task<Role?> FindByIdWithPermissionsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Read-only. Ids that do not exist are simply missing from the result.</summary>
    Task<IReadOnlyList<Role>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    Task<bool> NameExistsAsync(string normalizedName, Guid? excludingRoleId = null, CancellationToken ct = default);

    Task<int> CountUsersAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>Returns false when another role already has the same normalized name.</summary>
    Task<bool> CreateAsync(Role role, CancellationToken ct = default);

    /// <summary>Saves changes made to a tracked role. Returns false on a duplicate name.</summary>
    Task<bool> UpdateAsync(Role role, CancellationToken ct = default);

    Task DeleteAsync(Role role, CancellationToken ct = default);

    /// <summary>Makes the role's permission links match exactly the given permission ids.</summary>
    Task ReplacePermissionsAsync(Role role, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default);
}