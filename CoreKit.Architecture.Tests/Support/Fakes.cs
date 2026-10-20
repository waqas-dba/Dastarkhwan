namespace CoreKit.Architecture.Tests.Support;

public sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }

    public bool IsAuthenticated => UserId is not null;
}

public sealed class FakeScopeContext : IAccessScopeContext
{
    public AccessScope? Current { get; set; }
}

/// <summary>Membership the test controls directly. Counts lookups, so tests can see what the cache saved.</summary>
public sealed class FakeMembership : IAccessMembership
{
    private readonly Dictionary<(AccessScope, Guid), AccessMember> _members = new();

    public int Lookups { get; private set; }

    public void Set(AccessScope scope, Guid userId, bool isOwner = false)
        => _members[(scope, userId)] = new AccessMember(isOwner);

    public void Remove(AccessScope scope, Guid userId) => _members.Remove((scope, userId));

    public Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        Lookups++;
        return Task.FromResult(_members.TryGetValue((scope, userId), out var member) ? member : null);
    }
}

public sealed class FakeCurrentTenant : ICurrentTenant
{
    public Guid? Id { get; set; }

    public bool IsResolved => Id is not null;

    public Guid RequiredId => Id ?? throw new TenantNotResolvedException();

    public IDisposable Change(Guid? tenantId) => throw new NotSupportedException();
}

public sealed class FakeTenantMembershipReader : ITenantMembershipReader
{
    public Dictionary<(Guid TenantId, Guid UserId), bool> Members { get; } = new();

    public Task<TenantMemberInfo?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(Members.TryGetValue((tenantId, userId), out var isOwner) ? new TenantMemberInfo(isOwner) : null);
}

internal static class ResultAssert
{
    public static T Ok<T>(AccessResult<T> result)
    {
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    public static void Ok(AccessResult result) => Assert.True(result.IsSuccess, result.Error?.Message);

    public static void Fails(AccessResult result, AccessErrorKind kind, string? code = null)
    {
        Assert.True(result.IsFailure, "Expected a failure but the call succeeded.");
        Assert.Equal(kind, result.Error!.Kind);

        if (code is not null)
            Assert.Equal(code, result.Error.Code);
    }
}