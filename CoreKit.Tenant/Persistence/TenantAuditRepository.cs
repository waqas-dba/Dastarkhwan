namespace CoreKit.Tenant.Persistence;

internal interface ITenantAuditRepository
{
    void Add(TenantAuditEntry entry);

    /// <summary>Read-only, newest first.</summary>
    Task<(IReadOnlyList<TenantAuditEntry> Items, int TotalCount)> ListAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default);
}

internal sealed class TenantAuditRepository : ITenantAuditRepository
{
    private readonly TenantDbContext _db;

    public TenantAuditRepository(TenantDbContext db) => _db = db;

    public void Add(TenantAuditEntry entry) => _db.AuditEntries.Add(entry);

    public async Task<(IReadOnlyList<TenantAuditEntry> Items, int TotalCount)> ListAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.AuditEntries.AsNoTracking().Where(a => a.TenantId == tenantId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.OccurredAt).ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}