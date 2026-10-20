namespace CoreKit.Access.Abstractions;

/// <summary>The history of role and permission changes in the current scope. Needs access.audit.read.</summary>
public interface IAccessAuditLog
{
    /// <summary>Newest first.</summary>
    Task<AccessResult<AccessPagedResult<AccessAuditEntryDto>>> ListAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default);
}