using Microsoft.Extensions.Caching.Memory;

namespace CoreKit.Tenant.Services;

public interface ITenantInfoCache
{
    /// <summary>Forgets what is remembered about a tenant, by id and by each given slug.</summary>
    void Invalidate(Guid id, params string[] slugs);
}

/// <summary>
/// Finds a tenant's id, slug and status quickly. The answer is remembered for a short time, and the
/// services forget it the moment a tenant changes. With several servers a change can take up to
/// Tenant:InfoCacheSeconds to reach the others.
/// </summary>
public sealed class TenantInfoProvider : ITenantInfoProvider, ITenantInfoCache
{
    private const string KeyPrefix = "corekit.tenant:";

    private readonly ITenantRepository _tenants;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public TenantInfoProvider(ITenantRepository tenants, IMemoryCache cache, IOptions<TenantOptions> options)
    {
        _tenants = tenants;
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(Math.Max(options.Value.InfoCacheSeconds, 0));
    }

    public async Task<TenantInfo?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(IdKey(id), out TenantInfo? cached) && cached is not null)
            return cached;

        return Remember(await _tenants.FindByIdAsync(id, ct));
    }

    public async Task<TenantInfo?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = TenantSlug.Normalize(slug);

        if (_cache.TryGetValue(SlugKey(normalized), out TenantInfo? cached) && cached is not null)
            return cached;

        return Remember(await _tenants.FindBySlugAsync(normalized, ct));
    }

    public void Invalidate(Guid id, params string[] slugs)
    {
        _cache.Remove(IdKey(id));

        foreach (var slug in slugs)
            _cache.Remove(SlugKey(TenantSlug.Normalize(slug)));
    }

    private TenantInfo? Remember(TenantEntity? tenant)
    {
        if (tenant is null)
            return null;

        var info = TenantMapper.ToInfo(tenant);

        if (_ttl > TimeSpan.Zero)
        {
            _cache.Set(IdKey(info.Id), info, _ttl);
            _cache.Set(SlugKey(info.Slug), info, _ttl);
        }

        return info;
    }

    private static string IdKey(Guid id) => $"{KeyPrefix}id:{id:N}";

    private static string SlugKey(string slug) => $"{KeyPrefix}slug:{slug}";
}