namespace CoreKit.Access.Tests.Support;

public sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

public sealed class FakeCurrentUser : IAccessCurrentUser
{
    public Guid? UserId { get; set; }

    public bool? IsAuthenticatedOverride { get; set; }

    public bool IsAuthenticated => IsAuthenticatedOverride ?? UserId is not null;
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

    public void Set(AccessScope scope, Guid userId, bool isOwner = false, long membershipGeneration = 1)
        => _members[(scope, userId)] = new AccessMember(isOwner, membershipGeneration);

    public void Remove(AccessScope scope, Guid userId) => _members.Remove((scope, userId));

    public Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        Lookups++;
        return Task.FromResult(_members.TryGetValue((scope, userId), out var member) ? member : null);
    }
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
