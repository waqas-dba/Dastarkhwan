using Microsoft.Extensions.Caching.Memory;

namespace CoreKit.Access.Services;

internal interface IAccessCache
{
    bool TryGet(AccessScope scope, Guid userId, out EffectiveAccess access);

    void Set(AccessScope scope, Guid userId, EffectiveAccess access);

    /// <summary>Forgets everyone's permissions in a scope (a role changed).</summary>
    void InvalidateScope(AccessScope scope);

    /// <summary>Forgets one person's permissions (their roles changed).</summary>
    void InvalidateUser(AccessScope scope, Guid userId);
}

/// <summary>
/// Remembers resolved permissions for a short time. A per-scope version number is part of every key,
/// so changing a role can forget a whole scope without listing its entries.
/// </summary>
internal sealed class AccessCache : IAccessCache
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public AccessCache(IMemoryCache cache, IOptions<AccessOptions> options)
    {
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(Math.Max(options.Value.CacheSeconds, 0));
    }

    public bool TryGet(AccessScope scope, Guid userId, out EffectiveAccess access)
    {
        if (_ttl > TimeSpan.Zero &&
            _cache.TryGetValue(UserKey(scope, userId), out EffectiveAccess? cached) &&
            cached is not null)
        {
            access = cached;
            return true;
        }

        access = EffectiveAccess.None;
        return false;
    }

    public void Set(AccessScope scope, Guid userId, EffectiveAccess access)
    {
        if (_ttl > TimeSpan.Zero)
            _cache.Set(UserKey(scope, userId), access, _ttl);
    }

    public void InvalidateScope(AccessScope scope)
        => _cache.Set(VersionKey(scope), Version(scope) + 1);

    public void InvalidateUser(AccessScope scope, Guid userId)
        => _cache.Remove(UserKey(scope, userId));

    private long Version(AccessScope scope) => _cache.GetOrCreate(VersionKey(scope), _ => 0L);

    private string UserKey(AccessScope scope, Guid userId) => $"corekit.access:{scope}:v{Version(scope)}:{userId:N}";

    private static string VersionKey(AccessScope scope) => $"corekit.access.version:{scope}";
}