namespace CoreKit.Access.Persistence;

/// <summary>Access-owned persistence contract for tenant membership projection state.</summary>
internal interface IAccessMembershipProjectionRepository
{
    Task<AccessMembershipProjection?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<AccessMembershipProjection>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    void Add(AccessMembershipProjection projection);
}

internal sealed class AccessMembershipProjectionRepository : IAccessMembershipProjectionRepository
{
    private readonly AccessDbContext _db;

    public AccessMembershipProjectionRepository(AccessDbContext db) => _db = db;

    public Task<AccessMembershipProjection?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _db.MembershipProjections
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId, ct);

    public async Task<IReadOnlyList<AccessMembershipProjection>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.MembershipProjections.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.UserId)
            .ToListAsync(ct);

    public void Add(AccessMembershipProjection projection) => _db.MembershipProjections.Add(projection);
}
