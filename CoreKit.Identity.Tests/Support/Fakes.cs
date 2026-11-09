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
