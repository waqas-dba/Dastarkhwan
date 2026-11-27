using CoreKit.Access.Contracts;

namespace CoreKit.Access.Abstractions;

/// <summary>
/// Who a background job acts for. A job never carries an actor as plain data; the actor comes from a
/// sealed grant that was issued during an authorized request, or from trusted host code for system work.
/// </summary>
public enum AccessBackgroundActorKind
{
    /// <summary>The job acts for a real, authenticated user. Their permissions are re-checked at run time.</summary>
    User = 1,

    /// <summary>The job acts as the system. It is not a user: user-permission checks deny it.</summary>
    System = 2
}

/// <summary>
/// The explicit, trusted context of one background execution. It is built from a redeemed grant (or from
/// trusted host code for a system run), never read from a job payload.
/// </summary>
/// <param name="JobKind">The kind of job this context is valid for.</param>
/// <param name="ActorKind">Whether the job acts for a user or for the system.</param>
/// <param name="ActorUserId">The acting user's id for a user job; null for a system job.</param>
/// <param name="TenantScope">
/// The tenant the job runs in, or null for a platform-level job. Tenant scope and platform scope never
/// fall back to each other.
/// </param>
public sealed record AccessBackgroundExecution(
    string JobKind,
    AccessBackgroundActorKind ActorKind,
    Guid? ActorUserId,
    AccessScope? TenantScope);

/// <summary>
/// Read-only view of the background context of the current DI scope. While no background context is active
/// the answer is "not active", and the normal request context is used instead.
/// </summary>
public interface IAccessBackgroundContext
{
    bool IsActive { get; }

    AccessBackgroundExecution? Current { get; }
}

/// <summary>
/// An opaque, tamper-proof, expiring capability that carries a validated tenant scope and actor into a
/// background job. It is encrypted and signed with the application's Data Protection keys, so a forged or
/// edited value cannot be turned into a valid grant. Treat it like a secret: do not log it, and do not
/// return it to a browser.
/// </summary>
public readonly record struct AccessBackgroundGrantToken(string Value)
{
    public override string ToString() => "[access background grant]";
}

/// <summary>
/// The outcome of running background work. Authorization failures are values, not exceptions, so a job
/// runner can record them and retry or drop the job deliberately.
/// </summary>
public enum AccessBackgroundRunStatus
{
    /// <summary>The work ran to completion. Exceptions thrown by the work are not swallowed.</summary>
    Completed = 1,

    /// <summary>Not a valid grant (unreadable, tampered, wrong version, or from another key ring).</summary>
    GrantInvalid = 2,

    /// <summary>The grant's expiry time has passed.</summary>
    GrantExpired = 3,

    /// <summary>The grant was issued for a different job kind or a different required permission.</summary>
    GrantMismatch = 4,

    /// <summary>The actor no longer holds the required permission. The work did not run.</summary>
    NotAuthorized = 5,

    /// <summary>The requested execution context is malformed. The work did not run.</summary>
    ContextInvalid = 6,
    /// <summary>The grant was already redeemed. Replay rejected.</summary>
    GrantAlreadyConsumed = 7
}

/// <summary>
/// Issues background grants. A grant can only be issued from inside an authenticated, tenant-validated
/// request, and only while the caller holds the required permission on fresh data.
/// </summary>
public interface IAccessBackgroundGrantIssuer
{
    /// <summary>
    /// Call from an authorized request. The scope and actor are read from the request context, never from
    /// the caller's input. The caller must currently hold <paramref name="requiredPermission"/> (checked on
    /// fresh data). Refused inside a background context, so a running job cannot re-issue authority.
    /// </summary>
    /// <param name="jobKind">A short, stable name for the job. The runner must pass the same value later.</param>
    /// <param name="requiredPermission">The permission the actor must hold now and again when the job runs.</param>
    /// <param name="lifetime">How long the grant stays valid. Defaults to the configured lifetime.</param>
    Task<AccessResult<AccessBackgroundGrantToken>> IssueAsync(
        string jobKind, string requiredPermission, TimeSpan? lifetime = null, CancellationToken ct = default);
}

/// <summary>
/// Runs background work inside a fresh DI scope whose Access scope and actor come from the grant (or from
/// trusted host code for system work). The context is removed afterwards, so nothing leaks between jobs.
/// </summary>
public interface IAccessBackgroundExecutor
{
    /// <summary>
    /// Redeems <paramref name="grant"/>, opens a new DI scope, re-checks <paramref name="requiredPermission"/>
    /// on fresh data, runs <paramref name="work"/>, and always removes the background context afterwards.
    /// </summary>
    Task<AccessBackgroundRunStatus> RunAsync(
        AccessBackgroundGrantToken grant,
        string expectedJobKind,
        string requiredPermission,
        Func<IServiceProvider, CancellationToken, Task> work,
        CancellationToken ct = default);

    /// <summary>
    /// Runs trusted system work. <paramref name="tenantScope"/> must come from trusted host code (for example
    /// by iterating the tenant store), never from a job payload. The system actor is not a user, so guarded
    /// user-permission checks deny it; system work must call trusted maintenance services deliberately.
    /// </summary>
    Task<AccessBackgroundRunStatus> RunAsSystemAsync(
        string jobKind,
        string source,
        AccessScope? tenantScope,
        Func<IServiceProvider, CancellationToken, Task> work,
        CancellationToken ct = default);
}

public static class AccessBackgroundExecutorCompatExtensions
{
    [Obsolete("Pass a `source` argument to audit the caller. Will be removed in v2.")]
    public static Task<AccessBackgroundRunStatus> RunAsSystemAsync(
        this IAccessBackgroundExecutor executor,
        string jobKind,
        AccessScope? tenantScope,
        Func<IServiceProvider, CancellationToken, Task> work,
        CancellationToken ct = default)
        => executor.RunAsSystemAsync(
            jobKind, "legacy.RunAsSystemAsync", tenantScope, work, ct);
}