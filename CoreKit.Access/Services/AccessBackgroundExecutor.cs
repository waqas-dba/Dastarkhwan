namespace CoreKit.Access.Services;

/// <summary>
/// Runs background work with an explicit Access context. Each run gets its own DI scope and its own context
/// holder, so concurrent jobs never see each other's scope or actor, and the context is always removed
/// afterwards even when the work throws.
/// </summary>
internal sealed class AccessBackgroundExecutor : IAccessBackgroundExecutor
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AccessBackgroundGrantProtector _protector;
    private readonly TimeProvider _time;

    public AccessBackgroundExecutor(
        IServiceScopeFactory scopes, AccessBackgroundGrantProtector protector, TimeProvider time)
    {
        _scopes = scopes;
        _protector = protector;
        _time = time;
    }

    public async Task<AccessBackgroundRunStatus> RunAsync(
        AccessBackgroundGrantToken grant,
        string expectedJobKind,
        string requiredPermission,
        Func<IServiceProvider, CancellationToken, Task> work,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (string.IsNullOrWhiteSpace(requiredPermission))
            return AccessBackgroundRunStatus.ContextInvalid;

        // Redeem before any scope exists: an unreadable, tampered or expired grant must not create work.
        var redeemed = _protector.Redeem(grant.Value, expectedJobKind, requiredPermission, _time.GetUtcNow().UtcDateTime);
        if (redeemed.Failure is { } failure)
            return failure;

        var execution = redeemed.Execution!;

        // A new scope per job: fresh scoped services and a fresh context holder, so nothing leaks between jobs.
        await using var scope = _scopes.CreateAsyncScope();
        var holder = scope.ServiceProvider.GetRequiredService<AccessBackgroundContextHolder>();
        holder.Activate(execution);

        try
        {
            // The grant was authorized when it was issued; the actor may have lost the permission since. The
            // check is repeated on fresh data, so revoking access between enqueue and run stops the job.
            if (scope.ServiceProvider.GetRequiredService<IAccessService>() is not IFreshAccessSource fresh)
                return AccessBackgroundRunStatus.NotAuthorized;

            // No tenant scope means a platform-level job, checked against the fixed platform scope.
            var checkScope = execution.TenantScope ?? AccessScope.ForPlatform(AccessScope.PlatformId);
            var access = await fresh.GetFreshAccessAsync(execution.ActorUserId!.Value, checkScope, ct);
            if (!access.Has(requiredPermission))
                return AccessBackgroundRunStatus.NotAuthorized;

            await work(scope.ServiceProvider, ct);
            return AccessBackgroundRunStatus.Completed;
        }
        finally
        {
            // Always remove the context, even if the work threw.
            holder.Deactivate();
        }
    }

    public async Task<AccessBackgroundRunStatus> RunAsSystemAsync(
        string jobKind,
        AccessScope? tenantScope,
        Func<IServiceProvider, CancellationToken, Task> work,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        var execution = new AccessBackgroundExecution(jobKind, AccessBackgroundActorKind.System, null, tenantScope);
        if (!AccessBackgroundRules.IsValid(execution))
            return AccessBackgroundRunStatus.ContextInvalid;

        await using var scope = _scopes.CreateAsyncScope();
        var holder = scope.ServiceProvider.GetRequiredService<AccessBackgroundContextHolder>();
        holder.Activate(execution);

        try
        {
            // The system actor is not a user, so user-permission checks deny it. System work must call trusted,
            // unguarded maintenance services deliberately.
            await work(scope.ServiceProvider, ct);
            return AccessBackgroundRunStatus.Completed;
        }
        finally
        {
            holder.Deactivate();
        }
    }
}