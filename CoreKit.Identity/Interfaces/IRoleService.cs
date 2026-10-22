using CoreKit.Identity.Models;

namespace CoreKit.Identity.Interfaces;

public interface IRoleService
{
    Task<IdentityResult<IReadOnlyList<RoleDto>>> ListAsync(CancellationToken ct = default);

    Task<IdentityResult<RoleDto>> GetAsync(Guid id, CancellationToken ct = default);

    Task<IdentityResult<RoleDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);

    Task<IdentityResult<RoleDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default);

    Task<Common.IdentityResult> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Replaces the role's permissions with exactly the given set.</summary>
    Task<IdentityResult<RoleDto>> SetPermissionsAsync(Guid id, SetRolePermissionsRequest request, CancellationToken ct = default);
}