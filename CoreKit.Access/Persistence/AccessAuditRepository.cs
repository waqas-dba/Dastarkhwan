namespace CoreKit.Access.Persistence;

internal interface IAccessAuditRepository
{
    void Add(AccessAuditEntry entry);

    /// <summary>Read-only audit entries for one scope, newest first, paginated.</summary>
    Task<(IReadOnlyList<AccessAuditEntry> Items, int TotalCount)> ListAsync(
        AccessScope scope, int page, int pageSize, CancellationToken ct = default);
}

internal sealed class AccessAuditRepository : IAccessAuditRepository
{
    private readonly AccessDbContext _db;

    public AccessAuditRepository(AccessDbContext db) => _db = db;

    public void Add(AccessAuditEntry entry) => _db.AuditEntries.Add(entry);

    public async Task<(IReadOnlyList<AccessAuditEntry> Items, int TotalCount)> ListAsync(
        AccessScope scope, int page, int pageSize, CancellationToken ct = default)
    {
        var skip = (int)Math.Min(
            ((long)page - 1) * pageSize,
            int.MaxValue);

        var query = _db.AuditEntries.AsNoTracking()
            .Where(x => x.ScopeType == scope.Type && x.ScopeId == scope.Id);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}