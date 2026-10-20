namespace CoreKit.Access.Abstractions;

/// <summary>Which roles people hold in the current scope.</summary>
public interface IAccessAssignmentService
{
    /// <summary>Needs access.assignments.read.</summary>
    Task<AccessResult<IReadOnlyList<AccessRoleDto>>> GetRolesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Replaces the person's roles. Needs access.assignments.manage.</summary>
    Task<AccessResult<IReadOnlyList<AccessRoleDto>>> SetRolesAsync(
        Guid userId, SetAccessUserRolesRequest request, CancellationToken ct = default);
}