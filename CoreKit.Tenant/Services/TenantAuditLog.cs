namespace CoreKit.Tenant.Services;

public sealed class TenantAuditLog : ITenantAuditLog
{
    private readonly ITenantAuditRepository _audit;
    private readonly TenantAccessGuard _guard;

    public TenantAuditLog(ITenantAuditRepository audit, TenantAccessGuard guard)
    {
        _audit = audit;
        _guard = guard;
    }

    public async Task<TenantResult<TenantPagedResult<TenantAuditEntryDto>>> ListAsync(
        Guid tenantId, TenantAuditQuery query, CancellationToken ct = default)
    {
        if (_guard.EnsureCanAccess(tenantId) is { } denied)
            return denied;

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        // No existence check: the history of a deleted tenant stays readable for platform administrators.
        var (items, totalCount) = await _audit.ListAsync(tenantId, page, pageSize, ct);

        return TenantResult.Success(new TenantPagedResult<TenantAuditEntryDto>
        {
            Items = items.Select(e => TenantMapper.ToDto(e)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }
}