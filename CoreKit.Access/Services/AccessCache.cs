using Microsoft.Extensions.Caching.Memory;

namespace CoreKit.Access.Services;

/// <summary>
/// Which cache generation a permission lookup started in. If a role or an assignment changes while the lookup
/// reads the database, the generation moves on and the (now stale) result is not stored.
/// </summary>
internal readonly record struct AccessCacheToken(long ScopeVersion, long UserVersion);

internal interface IAccessCache
{
    bool TryGet(AccessScope scope, Guid userId, out EffectiveAccess access);

    void Set(AccessScope scope, Guid userId, EffectiveAccess access);

    /// <summary>Call before reading the database, and pass the token to the Set that stores the result.</summary>
    AccessCacheToken Begin(AccessScope scope, Guid userId);

    /// <summary>Stores the result only if nothing was invalidated since <paramref name="token"/> was taken.</summary>
    void Set(AccessScope scope, Guid userId, EffectiveAccess access, AccessCacheToken token);

    /// <summary>Forgets everyone's permissions in a scope (a role changed).</summary>
    void InvalidateScope(AccessScope scope);

    /// <summary>Forgets one person's permissions (their roles changed).</summary>
    void InvalidateUser(AccessScope scope, Guid userId);
}

/// <summary>
/// Remembers resolved permissions for a short time. A per-scope version and a per-user version are part of every
/// key, so changing a role can forget a whole scope, and changing one person's roles can forget just them,
/// without listing entries. The version counters expire when unused, so they do not pile up.
/// </summary>
internal sealed class AccessCache : IAccessCache
{
    private readonly object _gate = new();
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;
    private readonly MemoryCacheEntryOptions _versionOptions;

    public AccessCache(IMemoryCache cache, IOptions<AccessOptions> options)
    {
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(Math.Max(options.Value.CacheSeconds, 0));

        // A counter must outlive every entry made under it, so it can never fall back to 0 while an older entry
        // keyed with a smaller number is still remembered. Every lookup refreshes it (sliding expiration).
        var lifetime = _ttl * 2 > TimeSpan.FromMinutes(1) ? _ttl * 2 : TimeSpan.FromMinutes(1);
        _versionOptions = new MemoryCacheEntryOptions { SlidingExpiration = lifetime };
    }

    public bool TryGet(AccessScope scope, Guid userId, out EffectiveAccess access)
    {
        if (_ttl > TimeSpan.Zero &&
            _cache.TryGetValue(UserKey(scope, userId, Begin(scope, userId)), out EffectiveAccess? cached) &&
            cached is not null)
        {
            access = cached;
            return true;
        }

        access = EffectiveAccess.None;
        return false;
    }

    public void Set(AccessScope scope, Guid userId, EffectiveAccess access)
        => Set(scope, userId, access, Begin(scope, userId));

    public AccessCacheToken Begin(AccessScope scope, Guid userId)
        => _ttl > TimeSpan.Zero
            ? new AccessCacheToken(ReadVersion(ScopeVersionKey(scope)), ReadVersion(UserVersionKey(scope, userId)))
            : default;

    public void Set(AccessScope scope, Guid userId, EffectiveAccess access, AccessCacheToken token)
    {
        if (_ttl <= TimeSpan.Zero)
            return;

        lock (_gate)
        {
            // Something was invalidated after the lookup began: the result may be stale, so do not keep it.
            if (Begin(scope, userId) != token)
                return;

            _cache.Set(UserKey(scope, userId, token), access, _ttl);
        }
    }

    public void InvalidateScope(AccessScope scope)
    {
        if (_ttl > TimeSpan.Zero)
            Bump(ScopeVersionKey(scope));
    }

    public void InvalidateUser(AccessScope scope, Guid userId)
    {
        if (_ttl > TimeSpan.Zero)
            Bump(UserVersionKey(scope, userId));
    }

    private long ReadVersion(string key) => _cache.TryGetValue(key, out long version) ? version : 0L;

    private void Bump(string key)
    {
        lock (_gate)
        {
            _cache.Set(key, ReadVersion(key) + 1, _versionOptions);
        }
    }

    private static string UserKey(AccessScope scope, Guid userId, AccessCacheToken token)
        => $"corekit.access:{scope}:v{token.ScopeVersion}:u{token.UserVersion}:{userId:N}";

    private static string ScopeVersionKey(AccessScope scope) => $"corekit.access.version:{scope}";

    private static string UserVersionKey(AccessScope scope, Guid userId) => $"corekit.access.userversion:{scope}:{userId:N}";
}
