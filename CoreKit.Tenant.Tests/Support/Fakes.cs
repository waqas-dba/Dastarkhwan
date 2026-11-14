namespace CoreKit.Tenant.Tests.Support;

public sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

public sealed class FakeTenantActor : ITenantActor
{
    public Guid? UserId { get; set; }

    public bool IsPlatformAdmin { get; set; }

    public Task<bool> IsPlatformAdminAsync(CancellationToken ct = default) => Task.FromResult(IsPlatformAdmin);
}

public sealed class FakePermissionChecker : CoreKit.Access.Abstractions.IPermissionChecker
{
    private readonly ISet<string> _permissions = new HashSet<string>(StringComparer.Ordinal);

    public FakePermissionChecker(params string[] permissions)
    {
        foreach (var permission in permissions)
            _permissions.Add(permission);
    }

    public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
        => Task.FromResult(_permissions.Contains(permission));

    public Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default)
        => Task.FromResult(_permissions.Contains(permission));
}

/// <summary>Stands in for the host-provided current user from the shared kernel.</summary>
public sealed class FakeCurrentUserService : ICurrentUser
{
    public Guid? UserId { get; set; }

    public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();

    public bool IsAuthenticated => UserId is not null;
}

public sealed class RecordingHandler<TEvent> : ITenantEventHandler<TEvent> where TEvent : ITenantEvent
{
    private readonly List<ITenantEvent> _sink;

    public RecordingHandler(List<ITenantEvent> sink) => _sink = sink;

    public Task HandleAsync(TEvent tenantEvent, CancellationToken ct = default)
    {
        _sink.Add(tenantEvent);
        return Task.CompletedTask;
    }
}

public sealed class ThrowingHandler<TEvent> : ITenantEventHandler<TEvent> where TEvent : ITenantEvent
{
    private readonly Exception _exception;

    public ThrowingHandler(Exception exception) => _exception = exception;

    public Task HandleAsync(TEvent tenantEvent, CancellationToken ct = default) => throw _exception;
}

public static class ResultAssert
{
    public static T Ok<T>(TenantResult<T> result)
    {
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    public static void Ok(TenantResult result) => Assert.True(result.IsSuccess, result.Error?.Message);

    public static void Fails(TenantResult result, TenantErrorKind kind, string? code = null)
    {
        Assert.True(result.IsFailure, "Expected a failure but the call succeeded.");
        Assert.Equal(kind, result.Error!.Kind);

        if (code is not null)
            Assert.Equal(code, result.Error.Code);
    }
}