namespace CoreKit.Access.Persistence;

/// <summary>Access-owned persistence contract for tenant membership projection state.</summary>
internal interface IAccessMembershipProjectionRepository
{
    /// <summary>Tracked.</summary>
    Task<AccessMembershipProjection?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>Read-only. Used by authorization lookups, which must never modify the projection.</summary>
    Task<AccessMembershipProjection?> FindReadOnlyAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<AccessMembershipProjection>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Tracked. Used when a snapshot replaces a tenant's projection.</summary>
    Task<IReadOnlyList<AccessMembershipProjection>> ListByTenantForUpdateAsync(Guid tenantId, CancellationToken ct = default);

    void Add(AccessMembershipProjection projection);

    void Remove(AccessMembershipProjection projection);
}

internal sealed class AccessMembershipProjectionRepository : IAccessMembershipProjectionRepository
{
    private readonly AccessDbContext _db;

    public AccessMembershipProjectionRepository(AccessDbContext db) => _db = db;

    public Task<AccessMembershipProjection?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _db.MembershipProjections
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId, ct);

    public Task<AccessMembershipProjection?> FindReadOnlyAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _db.MembershipProjections.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId, ct);

    public async Task<IReadOnlyList<AccessMembershipProjection>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.MembershipProjections.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.UserId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AccessMembershipProjection>> ListByTenantForUpdateAsync(
        Guid tenantId, CancellationToken ct = default)
        => await _db.MembershipProjections
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.UserId)
            .ToListAsync(ct);

    public void Add(AccessMembershipProjection projection) => _db.MembershipProjections.Add(projection);

    public void Remove(AccessMembershipProjection projection) => _db.MembershipProjections.Remove(projection);
}