namespace CoreKit.Identity.Tests.Support;

/// <summary>Fixed clock so tests are deterministic.</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Fake current user read from claims, so tests control who is signed in.</summary>
public sealed class FakeCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = new List<string>();
    public IReadOnlyCollection<string> Permissions { get; set; } = new List<string>();

    public bool IsAuthenticated => UserId is not null;
}

/// <summary>
/// Controllable hasher for tests that must steer verification or rehash behaviour: every Verify
/// call returns the configured result, and every Hash call returns a deterministic marker.
/// </summary>
public sealed class StubHasher : IPasswordHasher
{
    private readonly PasswordCheckResult _verifyResult;

    public StubHasher(PasswordCheckResult verifyResult) => _verifyResult = verifyResult;

    public int HashCallCount { get; private set; }

    public string Hash(string password)
    {
        HashCallCount++;
        return "hash:" + password;
    }

    public PasswordCheckResult Verify(string passwordHash, string password) => _verifyResult;
}
