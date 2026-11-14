using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CoreKit.Tenant.Services;

/// <summary>
/// Answers "who belongs to this tenant" for other modules through the shared kernel.
/// It applies no caller checks of its own: it is server-side code and is never exposed over HTTP.
/// Results are cached for a short time (Tenant:MembershipCacheSeconds) and invalidated when
/// membership changes through <see cref="ITenantMembershipCache"/>.
/// </summary>
public sealed class TenantMembershipReader : ITenantMembershipReader, ITenantMembershipCache
{
    private const string KeyPrefix = "corekit.tenant:membership:";

    private readonly ITenantMemberRepository _members;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public TenantMembershipReader(ITenantMemberRepository members, IMemoryCache cache, IOptions<TenantOptions> options)
    {
        _members = members;
        _cache = cache;
        _ttl = TimeSpan.FromSeconds(Math.Max(options.Value.MembershipCacheSeconds, 0));
    }

    public async Task<TenantMemberInfo?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
            return null;

        var key = MakeKey(tenantId, userId);

        if (_ttl > TimeSpan.Zero && _cache.TryGetValue(key, out TenantMemberInfo? cached))
            return cached;

        var member = await _members.FindAsync(tenantId, userId, ct);

        var info = member is null ? null : new TenantMemberInfo(member.IsOwner, member.MembershipGeneration);

        if (_ttl > TimeSpan.Zero)
            _cache.Set(key, info, _ttl);

        return info;
    }

    public void Invalidate(Guid tenantId, Guid userId)
    {
        _cache.Remove(MakeKey(tenantId, userId));
    }

    private static string MakeKey(Guid tenantId, Guid userId)
        => $"{KeyPrefix}{tenantId:N}:{userId:N}";
}