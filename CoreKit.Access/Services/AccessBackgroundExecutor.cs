using CoreKit.Access.Abstractions;
using CoreKit.Access.Constants;
using CoreKit.Access.Contracts;
using CoreKit.Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreKit.Access.Services;

internal sealed class AccessBackgroundExecutor : IAccessBackgroundExecutor
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AccessBackgroundGrantProtector _protector;
    private readonly IGrantStore _grants;
    private readonly TimeProvider _time;
    private readonly ILogger<AccessBackgroundExecutor> _log;

    public AccessBackgroundExecutor(
        IServiceScopeFactory scopes,
        AccessBackgroundGrantProtector protector,
        IGrantStore grants,
        TimeProvider time,
        ILogger<AccessBackgroundExecutor> log)
    {
        _scopes = scopes;
        _protector = protector;
        _grants = grants;
        _time = time;
        _log = log;
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

        var redeemed = _protector.Redeem(
            grant.Value, expectedJobKind, requiredPermission, _time.GetUtcNow().UtcDateTime);
        if (redeemed.Failure is { } failure) return failure;

        if (!await _grants.TryConsumeAsync(redeemed.GrantId, _time.GetUtcNow().UtcDateTime, ct))
        {
            _log.LogWarning(
                "Access background grant {GrantId} for job {JobKind} rejected: already consumed.",
                redeemed.GrantId, expectedJobKind);
            return AccessBackgroundRunStatus.GrantAlreadyConsumed;
        }

        var execution = redeemed.Execution!;
        await using var scope = _scopes.CreateAsyncScope();
        var holder = scope.ServiceProvider.GetRequiredService<AccessBackgroundContextHolder>();
        holder.Activate(execution);

        try
        {
            var access = scope.ServiceProvider.GetRequiredService<IAccessService>();
            var checkScope = execution.TenantScope ?? AccessScope.ForPlatform(AccessScope.PlatformId);

            var fresh = await access.GetFreshAccessAsync(execution.ActorUserId!.Value, checkScope, ct);
            if (!fresh.Has(requiredPermission))
                return AccessBackgroundRunStatus.NotAuthorized;

            await RecordRedemptionAsync(scope.ServiceProvider, checkScope, expectedJobKind,
                execution.ActorUserId!.Value, redeemed.GrantId, ct);

            await work(scope.ServiceProvider, ct);
            return AccessBackgroundRunStatus.Completed;
        }
        finally
        {
            holder.Deactivate();
        }
    }

    public async Task<AccessBackgroundRunStatus> RunAsSystemAsync(
        string jobKind,
        string source,
        AccessScope? tenantScope,
        Func<IServiceProvider, CancellationToken, Task> work,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (string.IsNullOrWhiteSpace(source) || source.Length > 200)
            return AccessBackgroundRunStatus.ContextInvalid;

        var execution = new AccessBackgroundExecution(
            jobKind, AccessBackgroundActorKind.System, null, tenantScope);
        if (!AccessBackgroundRules.IsValid(execution))
            return AccessBackgroundRunStatus.ContextInvalid;

        await using var scope = _scopes.CreateAsyncScope();
        var holder = scope.ServiceProvider.GetRequiredService<AccessBackgroundContextHolder>();
        holder.Activate(execution);

        try
        {
            var auditScope = tenantScope ?? AccessScope.ForPlatform(AccessScope.PlatformId);

            await RecordSystemStartAsync(scope.ServiceProvider, auditScope, jobKind, source, ct);

            await work(scope.ServiceProvider, ct);
            return AccessBackgroundRunStatus.Completed;
        }
        finally
        {
            holder.Deactivate();
        }
    }

    private static async Task RecordRedemptionAsync(
        IServiceProvider sp, AccessScope scope, string jobKind, Guid actorId, Guid grantId,
        CancellationToken ct)
    {
        var audit = sp.GetRequiredService<IAccessAuditRecorder>();
        var db = sp.GetRequiredService<AccessDbContext>();
        audit.RecordSystem(
            scope,
            "access.background.redeemed",
            source: $"grant:{grantId:N}",
            details: $"job: {jobKind}; actor: {actorId}");
        await db.SaveChangesAsync(ct);
    }

    private static async Task RecordSystemStartAsync(
        IServiceProvider sp, AccessScope scope, string jobKind, string source,
        CancellationToken ct)
    {
        var audit = sp.GetRequiredService<IAccessAuditRecorder>();
        var db = sp.GetRequiredService<AccessDbContext>();
        audit.RecordSystem(
            scope,
            "access.background.system.started",
            source: source,
            details: $"job: {jobKind}");
        await db.SaveChangesAsync(ct);
    }
}