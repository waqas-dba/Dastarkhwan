namespace CoreKit.Access.Persistence;

/// <summary>Access-owned persistence for the tenant lifecycle state it authorizes against.</summary>
internal interface IAccessTenantStateRepository
{
    /// <summary>Tracked, so the consumer and the snapshot installer can update it.</summary>
    Task<AccessTenantState?> FindAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Read-only. Null when Access has no state for the tenant.</summary>
    Task<string?> GetStatusAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Every tenant Access holds anything for: a lifecycle state, a stream cursor or a projection row. Used to
    /// find tenants Tenant no longer has.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken ct = default);

    void Add(AccessTenantState state);
}

internal sealed class AccessTenantStateRepository : IAccessTenantStateRepository
{
    private readonly AccessDbContext _db;

    public AccessTenantStateRepository(AccessDbContext db) => _db = db;

    public Task<AccessTenantState?> FindAsync(Guid tenantId, CancellationToken ct = default)
        => _db.TenantStates.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

    public Task<string?> GetStatusAsync(Guid tenantId, CancellationToken ct = default)
        => _db.TenantStates.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => (string?)s.Status)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken ct = default)
        => await _db.TenantStates.Select(s => s.TenantId)
            .Union(_db.MembershipStreamCursors.Select(c => c.TenantId))
            .Union(_db.MembershipProjections.Select(p => p.TenantId))
            .ToListAsync(ct);

    public void Add(AccessTenantState state) => _db.TenantStates.Add(state);
}