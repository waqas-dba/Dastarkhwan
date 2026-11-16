using System.Diagnostics.Metrics;

namespace CoreKit.Tenant.Outbox;

/// <summary>One member row from Tenant's authoritative data, including a removed member as a tombstone.</summary>
public sealed record TenantProjectionSnapshotMember(Guid UserId, bool IsActive, long MembershipGeneration);

/// <summary>
/// A consistent picture of one tenant: its status, its event head, and every member (including removed ones).
/// </summary>
public sealed record TenantProjectionSnapshot(
    Guid TenantId, string Status, long HeadSequence, IReadOnlyList<TenantProjectionSnapshotMember> Members);

/// <summary>What the downstream projection currently holds for one tenant.</summary>
public sealed record TenantProjectionRemoteView(
    Guid TenantId, long LastAppliedSequence, string? TenantStatus, IReadOnlyList<TenantProjectionSnapshotMember> Members);

public enum TenantProjectionInstallOutcome
{
    Installed = 1,
    Repaired = 2,
    Unchanged = 3,
    IgnoredStale = 4,
    RetryLater = 5,
    Rejected = 6
}

/// <summary>
/// Tenant-owned port to the downstream projection, used only by recovery and reconciliation. The
/// implementation lives with the consumer (Access) and calls its public installer and reader.
/// </summary>
public interface ITenantProjectionGateway
{
    Task<TenantProjectionInstallOutcome> InstallAsync(TenantProjectionSnapshot snapshot, CancellationToken ct);

    Task<TenantProjectionRemoteView?> GetViewAsync(Guid tenantId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> ListKnownTenantIdsAsync(CancellationToken ct);
}

public enum TenantSnapshotReadStatus
{
    Found = 1,
    TenantNotFound = 2,
    Contended = 3
}

public sealed record TenantSnapshotRead(TenantSnapshotReadStatus Status, TenantProjectionSnapshot? Snapshot);

/// <summary>
/// Reads a snapshot that is consistent with its sequence number without special isolation levels: the tenant's
/// head (sequence and status) is read before and after the members. Every membership or status mutation bumps
/// the sequence in the same transaction as the change, so if both reads match, nothing committed in between.
/// </summary>
public sealed class TenantProjectionSnapshotReader
{
    private const int MaxAttempts = 5;

    private readonly TenantDbContext _db;

    public TenantProjectionSnapshotReader(TenantDbContext db) => _db = db;

    public async Task<TenantSnapshotRead> ReadAsync(Guid tenantId, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var before = await HeadAsync(tenantId, ct);
            if (before is null)
                return new TenantSnapshotRead(TenantSnapshotReadStatus.TenantNotFound, null);

            // Removed members are included on purpose: their generation is the floor a rejoin must exceed.
            var members = await _db.Members.IgnoreQueryFilters().AsNoTracking()
                .Where(m => m.TenantId == tenantId)
                .OrderBy(m => m.UserId)
                .Select(m => new TenantProjectionSnapshotMember(m.UserId, m.RemovedAt == null, m.MembershipGeneration))
                .ToListAsync(ct);

            var after = await HeadAsync(tenantId, ct);
            if (after is null)
                return new TenantSnapshotRead(TenantSnapshotReadStatus.TenantNotFound, null);

            if (after.Value.Sequence == before.Value.Sequence && after.Value.Status == before.Value.Status)
            {
                return new TenantSnapshotRead(
                    TenantSnapshotReadStatus.Found,
                    new TenantProjectionSnapshot(tenantId, before.Value.Status.ToString(), before.Value.Sequence, members));
            }
        }

        return new TenantSnapshotRead(TenantSnapshotReadStatus.Contended, null);
    }

    private async Task<(TenantStatus Status, long Sequence)?> HeadAsync(Guid tenantId, CancellationToken ct)
    {
        var row = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Status, t.LastEventSequence })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : (row.Status, row.LastEventSequence);
    }
}

public enum TenantProjectionDriftKind
{
    /// <summary>Access has no record of the tenant.</summary>
    NotKnownToAccess = 1,

    /// <summary>Access is behind the snapshot: events are still in flight. Not drift.</summary>
    InFlight = 2,

    /// <summary>Access is AHEAD of Tenant's head. Never auto-repaired (possible Tenant restore or rollback).</summary>
    AccessAhead = 3,

    InSync = 4,

    /// <summary>Same sequence, different content.</summary>
    Drift = 5
}

public sealed record TenantProjectionDiff(
    TenantProjectionDriftKind Kind,
    bool StatusMismatch,
    IReadOnlyList<Guid> MissingInAccess,
    IReadOnlyList<Guid> ExtraInAccess,
    IReadOnlyList<Guid> MismatchedMembers)
{
    public int DriftCount => (StatusMismatch ? 1 : 0) + MissingInAccess.Count + ExtraInAccess.Count + MismatchedMembers.Count;
}

public static class TenantProjectionComparer
{
    private static readonly IReadOnlyList<Guid> None = Array.Empty<Guid>();

    public static TenantProjectionDiff Compare(TenantProjectionSnapshot snapshot, TenantProjectionRemoteView? view)
    {
        if (view is null)
        {
            return new TenantProjectionDiff(
                TenantProjectionDriftKind.NotKnownToAccess, true, snapshot.Members.Select(m => m.UserId).ToList(), None, None);
        }

        if (view.LastAppliedSequence < snapshot.HeadSequence)
            return new TenantProjectionDiff(TenantProjectionDriftKind.InFlight, false, None, None, None);

        if (view.LastAppliedSequence > snapshot.HeadSequence)
            return new TenantProjectionDiff(TenantProjectionDriftKind.AccessAhead, false, None, None, None);

        // Same sequence: the two must agree exactly, and Tenant is authoritative.
        var statusMismatch = !string.Equals(view.TenantStatus, snapshot.Status, StringComparison.Ordinal);
        var remote = view.Members.ToDictionary(m => m.UserId);
        var local = snapshot.Members.ToDictionary(m => m.UserId);

        var missing = local.Keys.Where(id => !remote.ContainsKey(id)).ToList();
        var extra = remote.Keys.Where(id => !local.ContainsKey(id)).ToList();
        var mismatched = local
            .Where(kv => remote.TryGetValue(kv.Key, out var r)
                         && (r.IsActive != kv.Value.IsActive || r.MembershipGeneration != kv.Value.MembershipGeneration))
            .Select(kv => kv.Key)
            .ToList();

        var drift = statusMismatch || missing.Count > 0 || extra.Count > 0 || mismatched.Count > 0;

        return new TenantProjectionDiff(
            drift ? TenantProjectionDriftKind.Drift : TenantProjectionDriftKind.InSync,
            statusMismatch, missing, extra, mismatched);
    }
}

public sealed record TenantReconciliationReport(
    Guid TenantId, TenantProjectionDriftKind Kind, int DriftCount, bool Repaired, TenantProjectionInstallOutcome? RepairOutcome);

public sealed record TenantReconciliationSummary(
    int Checked, int InSync, int InFlight, int Drift, int NotKnown, int Repaired,
    IReadOnlyList<Guid> AccessAheadTenants,
    IReadOnlyList<Guid> OrphanedInAccess,
    IReadOnlyList<Guid> Failed);

public sealed record TenantProjectionBootstrapReport(
    int Tenants, int Installed, int Repaired, int Unchanged, int IgnoredStale, IReadOnlyList<Guid> Failed);

/// <summary>
/// Platform-only recovery operations. Tenant is authoritative for membership and status; the Access projection
/// is derived. Every operation is idempotent, repeatable and never moves Access backwards.
/// </summary>
public sealed class TenantProjectionRecoveryService
{
    private const int PageSize = 200;
    private const string RequeueAuditAction = "tenant.outbox_requeued";

    private static readonly Meter Meter = new("CoreKit.Tenant.Projection");
    private static readonly Counter<long> InstalledCounter = Meter.CreateCounter<long>("projection.install");
    private static readonly Counter<long> DriftCounter = Meter.CreateCounter<long>("projection.reconcile");

    private readonly TenantDbContext _db;
    private readonly TenantProjectionSnapshotReader _reader;
    private readonly ITenantProjectionGateway _gateway;
    private readonly TenantAccessGuard _guard;
    private readonly ITenantAuditRecorder _audit;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly ILogger<TenantProjectionRecoveryService> _logger;

    public TenantProjectionRecoveryService(
        TenantDbContext db,
        TenantProjectionSnapshotReader reader,
        ITenantProjectionGateway gateway,
        TenantAccessGuard guard,
        ITenantAuditRecorder audit,
        ITenantUnitOfWork unitOfWork,
        TimeProvider time,
        ILogger<TenantProjectionRecoveryService> logger)
    {
        _db = db;
        _reader = reader;
        _gateway = gateway;
        _guard = guard;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _time = time;
        _logger = logger;
    }

    /// <summary>Rebuilds one tenant's projection from Tenant's authoritative data.</summary>
    public async Task<TenantResult<TenantProjectionInstallOutcome>> RebuildTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        if (await _guard.EnsurePlatformScopeAsync(ct) is { } denied)
            return denied;

        var read = await _reader.ReadAsync(tenantId, ct);

        return read.Status switch
        {
            TenantSnapshotReadStatus.TenantNotFound => TenantErrors.NotFound("Tenant"),
            TenantSnapshotReadStatus.Contended => TenantErrors.Conflict("The tenant is changing too fast to snapshot; try again."),
            _ => TenantResult.Success(await InstallAsync(read.Snapshot!, ct))
        };
    }

    /// <summary>
    /// Bootstrap: installs every tenant's snapshot. Safe to repeat and to resume after an interruption. A tenant
    /// that fails is reported and does not stop the run.
    /// </summary>
    public async Task<TenantResult<TenantProjectionBootstrapReport>> RebuildAllAsync(CancellationToken ct = default)
    {
        if (await _guard.EnsurePlatformScopeAsync(ct) is { } denied)
            return denied;

        int tenants = 0, installed = 0, repaired = 0, unchanged = 0, stale = 0;
        var failed = new List<Guid>();

        await foreach (var tenantId in AllTenantIdsAsync(ct))
        {
            tenants++;
            var read = await _reader.ReadAsync(tenantId, ct);

            if (read.Status == TenantSnapshotReadStatus.TenantNotFound)
            {
                tenants--;
                continue;
            }

            if (read.Status != TenantSnapshotReadStatus.Found)
            {
                failed.Add(tenantId);
                continue;
            }

            switch (await InstallAsync(read.Snapshot!, ct))
            {
                case TenantProjectionInstallOutcome.Installed: installed++; break;
                case TenantProjectionInstallOutcome.Repaired: repaired++; break;
                case TenantProjectionInstallOutcome.Unchanged: unchanged++; break;
                case TenantProjectionInstallOutcome.IgnoredStale: stale++; break;
                default: failed.Add(tenantId); break;
            }
        }

        return TenantResult.Success(new TenantProjectionBootstrapReport(tenants, installed, repaired, unchanged, stale, failed));
    }

    public async Task<TenantResult<TenantReconciliationReport>> ReconcileTenantAsync(
        Guid tenantId, bool repair, CancellationToken ct = default)
    {
        if (await _guard.EnsurePlatformScopeAsync(ct) is { } denied)
            return denied;

        var read = await _reader.ReadAsync(tenantId, ct);

        return read.Status switch
        {
            TenantSnapshotReadStatus.TenantNotFound => TenantErrors.NotFound("Tenant"),
            TenantSnapshotReadStatus.Contended => TenantErrors.Conflict("The tenant is changing too fast to snapshot; try again."),
            _ => TenantResult.Success(await ReconcileCoreAsync(read.Snapshot!, repair, ct))
        };
    }

    public async Task<TenantResult<TenantReconciliationSummary>> ReconcileAllAsync(bool repair, CancellationToken ct = default)
    {
        if (await _guard.EnsurePlatformScopeAsync(ct) is { } denied)
            return denied;

        int checkedCount = 0, inSync = 0, inFlight = 0, drift = 0, notKnown = 0, repaired = 0;
        var ahead = new List<Guid>();
        var failed = new List<Guid>();
        var tenantIds = new HashSet<Guid>();

        await foreach (var tenantId in AllTenantIdsAsync(ct))
        {
            tenantIds.Add(tenantId);
            var read = await _reader.ReadAsync(tenantId, ct);

            if (read.Status != TenantSnapshotReadStatus.Found)
            {
                if (read.Status == TenantSnapshotReadStatus.Contended)
                    failed.Add(tenantId);
                continue;
            }

            checkedCount++;
            var report = await ReconcileCoreAsync(read.Snapshot!, repair, ct);

            switch (report.Kind)
            {
                case TenantProjectionDriftKind.InSync: inSync++; break;
                case TenantProjectionDriftKind.InFlight: inFlight++; break;
                case TenantProjectionDriftKind.AccessAhead: ahead.Add(tenantId); break;
                case TenantProjectionDriftKind.NotKnownToAccess: notKnown++; break;
                default: drift++; break;
            }

            if (report.Repaired)
                repaired++;
        }

        // Tenants Access knows but Tenant does not. Reported only: repairing means declaring them deleted, and
        // a restored or misconfigured Tenant database would make that destructive. That needs an owner decision.
        var orphans = (await _gateway.ListKnownTenantIdsAsync(ct)).Where(id => !tenantIds.Contains(id)).ToList();

        return TenantResult.Success(new TenantReconciliationSummary(
            checkedCount, inSync, inFlight, drift, notKnown, repaired, ahead, orphans, failed));
    }

    /// <summary>
    /// Makes a tenant's dead-lettered events deliverable again, after the cause was fixed. Safe to repeat: events
    /// Access already holds (for example because a snapshot covered them) are acknowledged as duplicates.
    /// </summary>
    public async Task<TenantResult<int>> RequeueDeadLettersAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (await _guard.EnsurePlatformScopeAsync(ct) is { } denied)
            return denied;

        var now = _time.GetUtcNow().UtcDateTime;

        var rows = await _db.OutboxMessages
            .Where(m => m.TenantId == tenantId && m.Status == TenantOutboxStatus.DeadLettered)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, TenantOutboxStatus.Pending)
                .SetProperty(m => m.AttemptCount, 0)
                .SetProperty(m => m.NextAttemptAt, now)
                .SetProperty(m => m.LeaseOwner, (string?)null)
                .SetProperty(m => m.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(m => m.DeadLetteredAt, (DateTime?)null), ct);

        if (rows == 0)
            return TenantResult.Success(0);

        _audit.Record(tenantId, RequeueAuditAction, $"messages: {rows}");

        if ((await _unitOfWork.SaveAsync(ct)).ToError() is { } saveError)
            return saveError;

        _logger.LogWarning("Requeued {Count} dead-lettered outbox message(s) for tenant {TenantId}.", rows, tenantId);
        return TenantResult.Success(rows);
    }

    private async Task<TenantReconciliationReport> ReconcileCoreAsync(
        TenantProjectionSnapshot snapshot, bool repair, CancellationToken ct)
    {
        var view = await _gateway.GetViewAsync(snapshot.TenantId, ct);
        var diff = TenantProjectionComparer.Compare(snapshot, view);

        DriftCounter.Add(1, new KeyValuePair<string, object?>("kind", diff.Kind.ToString()));

        if (diff.Kind == TenantProjectionDriftKind.AccessAhead)
        {
            _logger.LogError(
                "Access is ahead of Tenant for tenant {TenantId} (Access {Access}, Tenant {Tenant}). Not repaired automatically.",
                snapshot.TenantId, view!.LastAppliedSequence, snapshot.HeadSequence);
        }

        TenantProjectionInstallOutcome? outcome = null;
        var repaired = false;

        // Only equal-sequence drift and unknown tenants are repaired. In-flight is left to the pipeline.
        if (repair && diff.Kind is TenantProjectionDriftKind.Drift or TenantProjectionDriftKind.NotKnownToAccess)
        {
            outcome = await InstallAsync(snapshot, ct);
            repaired = outcome is TenantProjectionInstallOutcome.Installed or TenantProjectionInstallOutcome.Repaired;
        }

        return new TenantReconciliationReport(snapshot.TenantId, diff.Kind, diff.DriftCount, repaired, outcome);
    }

    private async Task<TenantProjectionInstallOutcome> InstallAsync(TenantProjectionSnapshot snapshot, CancellationToken ct)
    {
        var outcome = await _gateway.InstallAsync(snapshot, ct);
        InstalledCounter.Add(1, new KeyValuePair<string, object?>("outcome", outcome.ToString()));
        return outcome;
    }

    private async IAsyncEnumerable<Guid> AllTenantIdsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        for (var page = 0; ; page++)
        {
            var ids = await _db.Tenants.AsNoTracking()
                .OrderBy(t => t.Id)
                .Skip(page * PageSize)
                .Take(PageSize)
                .Select(t => t.Id)
                .ToListAsync(ct);

            foreach (var id in ids)
                yield return id;

            if (ids.Count < PageSize)
                yield break;
        }
    }
}

public static class TenantProjectionRecoveryExtensions
{
    /// <summary>
    /// Opt-in. Adds the platform-only recovery service and its snapshot reader. A host must also register an
    /// <see cref="ITenantProjectionGateway"/> (the consumer side, for example Access) before resolving the
    /// recovery service.
    /// </summary>
    public static IServiceCollection AddTenantProjectionRecovery(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<TenantProjectionSnapshotReader>();
        services.AddScoped<TenantProjectionRecoveryService>();
        return services;
    }
}