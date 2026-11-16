namespace CoreKit.Access.Services;

/// <summary>
/// Validation shared by everything that builds or redeems a background context. It keeps the rules in one
/// place so a grant, a system run and a direct context activation all accept exactly the same shapes.
/// </summary>
internal static class AccessBackgroundRules
{
    public const int MaxJobKindLength = 100;

    public static bool IsValidJobKind(string? jobKind)
        => !string.IsNullOrWhiteSpace(jobKind)
           && jobKind.Length <= MaxJobKindLength
           && !jobKind.Any(char.IsControl);

    public static bool IsValid(AccessBackgroundExecution? execution)
    {
        if (execution is null || !IsValidJobKind(execution.JobKind))
            return false;

        // A background context carries a tenant scope or no scope (a platform-level job). It never carries a
        // platform scope: a platform job is expressed as "no tenant scope" and is checked against the fixed
        // platform scope, so the two scope types can never be confused.
        if (execution.TenantScope is { } scope && (!scope.IsValid || scope.Type != AccessScope.TenantType))
            return false;

        return execution.ActorKind switch
        {
            // A user job must name a real, non-empty user id.
            AccessBackgroundActorKind.User => execution.ActorUserId is { } id && id != Guid.Empty,
            // A system job is not a user and must not carry a user id.
            AccessBackgroundActorKind.System => execution.ActorUserId is null,
            _ => false
        };
    }
}

/// <summary>
/// The background context of ONE DI scope. It is registered scoped, and every job gets its own scope, so
/// each job has its own holder: there is no static field and no AsyncLocal, and concurrent jobs cannot leak
/// a scope or an actor into each other.
/// </summary>
internal sealed class AccessBackgroundContextHolder : IAccessBackgroundContext
{
    public AccessBackgroundExecution? Current { get; private set; }

    public bool IsActive => Current is not null;

    public void Activate(AccessBackgroundExecution execution)
    {
        if (!AccessBackgroundRules.IsValid(execution))
            throw new ArgumentException("The background execution context is invalid.", nameof(execution));

        if (Current is not null)
            throw new InvalidOperationException("A background context is already active in this scope.");

        Current = execution;
    }

    public void Deactivate() => Current = null;
}

/// <summary>
/// While a background context is active the tenant scope comes from that context, and HttpContext is not
/// consulted at all. This matters for a job started inline inside a request: it must not silently inherit the
/// request's tenant.
/// </summary>
internal sealed class BackgroundAwareScopeContext : IAccessScopeContext
{
    private readonly IAccessScopeContext _inner;
    private readonly AccessBackgroundContextHolder _holder;

    public BackgroundAwareScopeContext(IAccessScopeContext inner, AccessBackgroundContextHolder holder)
    {
        _inner = inner;
        _holder = holder;
    }

    public AccessScope? Current => _holder.Current is { } execution ? execution.TenantScope : _inner.Current;
}

/// <summary>
/// A user actor is authenticated and carries the granting user's id. The system actor is neither: it has no
/// user id and is not authenticated, so every user-permission check fails closed for it.
/// </summary>
internal sealed class BackgroundAwareCurrentUser : IAccessCurrentUser
{
    private readonly IAccessCurrentUser _inner;
    private readonly AccessBackgroundContextHolder _holder;

    public BackgroundAwareCurrentUser(IAccessCurrentUser inner, AccessBackgroundContextHolder holder)
    {
        _inner = inner;
        _holder = holder;
    }

    public Guid? UserId => _holder.Current is { } execution
        ? (execution.ActorKind == AccessBackgroundActorKind.User ? execution.ActorUserId : null)
        : _inner.UserId;

    public bool IsAuthenticated => _holder.Current is { } execution
        ? execution.ActorKind == AccessBackgroundActorKind.User
        : _inner.IsAuthenticated;
}