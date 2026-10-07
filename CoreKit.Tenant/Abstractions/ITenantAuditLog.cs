namespace CoreKit.Tenant.Abstractions;

public interface ITenantAuditLog
{
    /// <summary>Newest first.</summary>
    Task<TenantResult<TenantPagedResult<TenantAuditEntryDto>>> ListAsync(
        Guid tenantId, TenantAuditQuery query, CancellationToken ct = default);
}