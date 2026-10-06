using CoreKit.IAM.Models;

namespace CoreKit.IAM.Interfaces;

public interface IRoleService
{
    Task<IamResult<IReadOnlyList<RoleDto>>> ListAsync(CancellationToken ct = default);

    Task<IamResult<RoleDto>> GetAsync(Guid id, CancellationToken ct = default);

    Task<IamResult<RoleDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);

    Task<IamResult<RoleDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default);

    Task<IamResult> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Replaces the role's permissions with exactly the given set.</summary>
    Task<IamResult<RoleDto>> SetPermissionsAsync(Guid id, SetRolePermissionsRequest request, CancellationToken ct = default);
}