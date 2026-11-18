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

    public bool IsAuthenticated => UserId is not null;
}

/// <summary>
/// Fake authorization port for tests. Configure which operations are allowed; everything else is denied.
/// </summary>
public sealed class FakeIdentityAdminAuthorizer : IIdentityAdminAuthorizer
{
    private readonly HashSet<IdentityAdminOperation> _allowed = new();

    public void Allow(IdentityAdminOperation operation) => _allowed.Add(operation);

    public void Deny(IdentityAdminOperation operation) => _allowed.Remove(operation);

    public void AllowAll()
    {
        _allowed.Add(IdentityAdminOperation.ReadUsers);
        _allowed.Add(IdentityAdminOperation.CreateUsers);
        _allowed.Add(IdentityAdminOperation.UpdateUsers);
        _allowed.Add(IdentityAdminOperation.DeleteUsers);
    }

    public void DenyAll() => _allowed.Clear();

    public Task<bool> IsAllowedAsync(IdentityAdminOperation operation, CancellationToken ct = default)
        => Task.FromResult(_allowed.Contains(operation));
}

/// <summary>
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
