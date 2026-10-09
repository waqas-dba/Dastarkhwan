namespace CoreKit.Tenant.Persistence;

public interface ITenantMemberRepository
{
    /// <summary>Read-only.</summary>
    Task<TenantMember?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>Tracked.</summary>
    Task<TenantMember?> FindForUpdateAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>Read-only, oldest member first.</summary>
    Task<IReadOnlyList<TenantMember>> ListAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Read-only, with each member's tenant, oldest membership first.</summary>
    Task<IReadOnlyList<TenantMember>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Tracked, oldest membership first.</summary>
    Task<IReadOnlyList<TenantMember>> ListForUserForUpdateAsync(Guid userId, CancellationToken ct = default);

    Task<int> CountOwnersAsync(Guid tenantId, CancellationToken ct = default);

    Task<bool> HasDefaultAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The user's default tenant, else their oldest one. Archived tenants are skipped.</summary>
    Task<Guid?> FindPreferredTenantIdAsync(Guid userId, CancellationToken ct = default);

    void Add(TenantMember member);

    void Remove(TenantMember member);
}

public sealed class TenantMemberRepository : ITenantMemberRepository
{
    private readonly TenantDbContext _db;

    public TenantMemberRepository(TenantDbContext db) => _db = db;

    public Task<TenantMember?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, ct);

    public Task<TenantMember?> FindForUpdateAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _db.Members.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, ct);

    public async Task<IReadOnlyList<TenantMember>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Members.AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .OrderBy(m => m.JoinedAt).ThenBy(m => m.UserId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TenantMember>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.Members.AsNoTracking()
            .Include(m => m.Tenant)
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.JoinedAt).ThenBy(m => m.TenantId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TenantMember>> ListForUserForUpdateAsync(Guid userId, CancellationToken ct = default)
        => await _db.Members
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.JoinedAt).ThenBy(m => m.TenantId)
            .ToListAsync(ct);

    public Task<int> CountOwnersAsync(Guid tenantId, CancellationToken ct = default)
        => _db.Members.CountAsync(m => m.TenantId == tenantId && m.IsOwner, ct);

    public Task<bool> HasDefaultAsync(Guid userId, CancellationToken ct = default)
        => _db.Members.AnyAsync(m => m.UserId == userId && m.IsDefault, ct);

    public Task<Guid?> FindPreferredTenantIdAsync(Guid userId, CancellationToken ct = default)
        => _db.Members.AsNoTracking()
            .Where(m => m.UserId == userId && m.Tenant.Status != TenantStatus.Archived)
            .OrderByDescending(m => m.IsDefault).ThenBy(m => m.JoinedAt).ThenBy(m => m.TenantId)
            .Select(m => (Guid?)m.TenantId)
            .FirstOrDefaultAsync(ct);

    public void Add(TenantMember member) => _db.Members.Add(member);

    public void Remove(TenantMember member) => _db.Members.Remove(member);
}