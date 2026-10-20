namespace CoreKit.Access.Abstractions;

/// <summary>Roles of the current scope. The caller needs access.roles.read or access.roles.manage.</summary>
public interface IAccessRoleService
{
    Task<AccessResult<IReadOnlyList<AccessPermissionDto>>> ListPermissionsAsync(CancellationToken ct = default);

    Task<AccessResult<IReadOnlyList<AccessRoleDto>>> ListAsync(CancellationToken ct = default);

    Task<AccessResult<AccessRoleDto>> GetAsync(Guid id, CancellationToken ct = default);

    Task<AccessResult<AccessRoleDto>> CreateAsync(CreateAccessRoleRequest request, CancellationToken ct = default);

    Task<AccessResult<AccessRoleDto>> UpdateAsync(Guid id, UpdateAccessRoleRequest request, CancellationToken ct = default);

    Task<AccessResult<AccessRoleDto>> SetPermissionsAsync(
        Guid id, SetAccessRolePermissionsRequest request, CancellationToken ct = default);

    Task<AccessResult> DeleteAsync(Guid id, CancellationToken ct = default);
}