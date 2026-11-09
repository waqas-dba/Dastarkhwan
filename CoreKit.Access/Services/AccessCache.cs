using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CoreKit.Access.Services;

/// <summary>
/// Which cache generation a permission lookup started in. If a role or an assignment changes while the lookup
/// reads the database, the generation moves on and the (now stale) result is not stored.
/// </summary>
internal readonly record struct AccessCacheToken(long Epoch, long ScopeVersion, long UserVersion);

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
/// Remembers resolved permissions for a short time. A per-scope generation and a per-user generation are part of every
/// key, so changing a role can forget a whole scope, and changing one person's roles can forget just them,
/// without listing entries. Generation metadata lives outside <see cref="IMemoryCache"/> so it never expires
/// unexpectedly, and is bounded by capacity; when exhausted, the epoch advances and all previous entries become
/// unreachable.
/// </summary>
internal sealed class AccessCache : IAccessCache, IDisposable
{
    private const int DefaultMaxGenerationEntries = 10_000;

    private readonly ReaderWriterLockSlim _gate = new();
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;
    private readonly int _maxGenerationEntries;

    // Generation metadata lives outside IMemoryCache so it never expires unexpectedly.
    private long _epoch;
    private long _sequence;
    private readonly Dictionary<AccessScope, long> _scopeGenerations = new();
    private readonly Dictionary<(AccessScope Scope, Guid UserId), long> _userGenerations = new();

    public AccessCache(IMemoryCache cache, IOptions<AccessOptions> options)
        : this(cache, options, DefaultMaxGenerationEntries)
    {
    }

    /// <summary>Internal test seam to inject a small generation metadata capacity.</summary>
    internal AccessCache(IMemoryCache cache, IOptions<AccessOptions> options, int maxGenerationEntries)
    {
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(Math.Max(options.Value.CacheSeconds, 0));
        _maxGenerationEntries = maxGenerationEntries;
    }

    public bool TryGet(AccessScope scope, Guid userId, out EffectiveAccess access)
    {
        if (_ttl <= TimeSpan.Zero)
        {
            access = EffectiveAccess.None;
            return false;
        }

        _gate.EnterReadLock();
        try
        {
            var token = GetCurrentToken(scope, userId);
            var key = MakeKey(scope, userId, token);

            if (_cache.TryGetValue(key, out EffectiveAccess? cached) && cached is not null)
            {
                access = cached;
                return true;
            }
        }
        finally
        {
            _gate.ExitReadLock();
        }

        access = EffectiveAccess.None;
        return false;
    }

    public void Set(AccessScope scope, Guid userId, EffectiveAccess access)
        => Set(scope, userId, access, Begin(scope, userId));

    public AccessCacheToken Begin(AccessScope scope, Guid userId)
    {
        if (_ttl <= TimeSpan.Zero)
            return default;

        _gate.EnterReadLock();
        try
        {
            return GetCurrentToken(scope, userId);
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    public void Set(AccessScope scope, Guid userId, EffectiveAccess access, AccessCacheToken token)
    {
        if (_ttl <= TimeSpan.Zero)
            return;

        _gate.EnterReadLock();
        try
        {
            // Something was invalidated after the lookup began: the result may be stale, so do not keep it.
            if (GetCurrentToken(scope, userId) != token)
                return;

            _cache.Set(MakeKey(scope, userId, token), access, _ttl);
        }
        finally
        {
            _gate.ExitReadLock();
        }
    }

    public void InvalidateScope(AccessScope scope)
    {
        if (_ttl <= TimeSpan.Zero)
            return;

        _gate.EnterWriteLock();
        try
        {
            if (_scopeGenerations.ContainsKey(scope))
            {
                _scopeGenerations[scope] = NextSequence();
            }
            else
            {
                EnsureCapacityForNewEntry();
                _scopeGenerations[scope] = NextSequence();
            }
        }
        finally
        {
            _gate.ExitWriteLock();
        }
    }

    public void InvalidateUser(AccessScope scope, Guid userId)
    {
        if (_ttl <= TimeSpan.Zero)
            return;

        var userKey = (scope, userId);

        _gate.EnterWriteLock();
        try
        {
            if (_userGenerations.ContainsKey(userKey))
            {
                _userGenerations[userKey] = NextSequence();
            }
            else
            {
                EnsureCapacityForNewEntry();
                _userGenerations[userKey] = NextSequence();
            }
        }
        finally
        {
            _gate.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private AccessCacheToken GetCurrentToken(AccessScope scope, Guid userId)
    {
        long scopeGen = _scopeGenerations.TryGetValue(scope, out var sg) ? sg : 0L;
        long userGen = _userGenerations.TryGetValue((scope, userId), out var ug) ? ug : 0L;
        return new AccessCacheToken(_epoch, scopeGen, userGen);
    }

    private static string MakeKey(AccessScope scope, Guid userId, AccessCacheToken token)
        => $"corekit.access:{scope}:e{token.Epoch}:v{token.ScopeVersion}:u{token.UserVersion}:{userId:N}";

    private long NextSequence()
    {
        checked { return ++_sequence; }
    }

    private void EnsureCapacityForNewEntry()
    {
        if (_scopeGenerations.Count + _userGenerations.Count >= _maxGenerationEntries)
        {
            checked { _epoch++; }
            _scopeGenerations.Clear();
            _userGenerations.Clear();
        }
    }

    /// <summary>Test-only seam: combined count of scope and user generation entries.</summary>
    internal int GenerationEntryCount => _scopeGenerations.Count + _userGenerations.Count;
}
