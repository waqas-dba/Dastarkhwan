namespace CoreKit.Tenant.Persistence;

internal interface ITenantSettingRepository
{
    /// <summary>Read-only, ordered by key.</summary>
    Task<IReadOnlyList<TenantSetting>> ListAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Read-only.</summary>
    Task<TenantSetting?> FindAsync(Guid tenantId, string key, CancellationToken ct = default);

    /// <summary>Tracked.</summary>
    Task<TenantSetting?> FindForUpdateAsync(Guid tenantId, string key, CancellationToken ct = default);

    /// <summary>Tracked. Keys that do not exist are simply missing from the result.</summary>
    Task<IReadOnlyList<TenantSetting>> FindManyForUpdateAsync(
        Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken ct = default);

    Task<int> CountAsync(Guid tenantId, CancellationToken ct = default);

    void Add(TenantSetting setting);

    void Remove(TenantSetting setting);
}

internal sealed class TenantSettingRepository : ITenantSettingRepository
{
    private readonly TenantDbContext _db;

    public TenantSettingRepository(TenantDbContext db) => _db = db;

    public async Task<IReadOnlyList<TenantSetting>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Settings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Key)
            .ToListAsync(ct);

    public Task<TenantSetting?> FindAsync(Guid tenantId, string key, CancellationToken ct = default)
        => _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key, ct);

    public Task<TenantSetting?> FindForUpdateAsync(Guid tenantId, string key, CancellationToken ct = default)
        => _db.Settings.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key, ct);

    public async Task<IReadOnlyList<TenantSetting>> FindManyForUpdateAsync(
        Guid tenantId, IReadOnlyCollection<string> keys, CancellationToken ct = default)
        => await _db.Settings
            .Where(s => s.TenantId == tenantId && keys.Contains(s.Key))
            .ToListAsync(ct);

    public Task<int> CountAsync(Guid tenantId, CancellationToken ct = default)
        => _db.Settings.CountAsync(s => s.TenantId == tenantId, ct);

    public void Add(TenantSetting setting) => _db.Settings.Add(setting);

    public void Remove(TenantSetting setting) => _db.Settings.Remove(setting);
}