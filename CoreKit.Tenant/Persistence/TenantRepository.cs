namespace CoreKit.Tenant.Persistence;

public interface ITenantRepository
{
    /// <summary>Read-only.</summary>
    Task<TenantEntity?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Read-only. The slug must already be normalized.</summary>
    Task<TenantEntity?> FindBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Tracked: change it, then save through the unit of work.</summary>
    Task<TenantEntity?> FindForUpdateAsync(Guid id, CancellationToken ct = default);

    Task<bool> SlugExistsAsync(string slug, Guid? excludingTenantId, CancellationToken ct = default);

    Task<(IReadOnlyList<TenantEntity> Items, int TotalCount)> ListAsync(
        string? search, TenantStatus? status, int page, int pageSize, CancellationToken ct = default);

    void Add(TenantEntity tenant);

    void Remove(TenantEntity tenant);
}

public sealed class TenantRepository : ITenantRepository
{
    private readonly TenantDbContext _db;

    public TenantRepository(TenantDbContext db) => _db = db;

    public Task<TenantEntity?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<TenantEntity?> FindBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public Task<TenantEntity?> FindForUpdateAsync(Guid id, CancellationToken ct = default)
        => _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> SlugExistsAsync(string slug, Guid? excludingTenantId, CancellationToken ct = default)
        => _db.Tenants.AnyAsync(
            t => t.Slug == slug && (excludingTenantId == null || t.Id != excludingTenantId.Value), ct);

    public async Task<(IReadOnlyList<TenantEntity> Items, int TotalCount)> ListAsync(
        string? search, TenantStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Tenants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(t => t.Slug.Contains(term) || t.Name.ToLower().Contains(term));
        }

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(t => t.Slug)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public void Add(TenantEntity tenant) => _db.Tenants.Add(tenant);

    public void Remove(TenantEntity tenant) => _db.Tenants.Remove(tenant);
}