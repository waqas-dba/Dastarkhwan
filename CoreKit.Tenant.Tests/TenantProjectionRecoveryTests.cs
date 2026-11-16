using CoreKit.Tenant.Outbox;

namespace CoreKit.Tenant.Tests;

/// <summary>
/// Proves the recovery operations: a consistent snapshot, a comparer that separates in-flight from real drift,
/// bootstrap, reconciliation that never moves Access backwards, and dead-letter requeueing.
/// </summary>
public sealed class TenantProjectionRecoveryTests : IDisposable
{
    private readonly TestEnv _env = new();
    private readonly FakeGateway _gateway = new();

    public void Dispose() => _env.Dispose();

    private TenantProjectionRecoveryService Service(TenantDbContext? db = null)
    {
        db ??= _env.NewContext();
        return new TenantProjectionRecoveryService(
            db, new TenantProjectionSnapshotReader(db), _gateway, _env.Guard, _env.Audit, _env.UnitOfWork, _env.Time,
            NullLogger<TenantProjectionRecoveryService>.Instance);
    }

    private static TenantProjectionSnapshot Snapshot(long head, string status, params TenantProjectionSnapshotMember[] members)
        => new(Guid.NewGuid(), status, head, members);

    [Fact]
    public async Task TheSnapshot_IsConsistentWithItsSequence_AndKeepsRemovedMembersAsTombstones()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        var active = Guid.NewGuid();
        var removed = Guid.NewGuid();
        _env.AddMember(tenant.Id, active);
        _env.AddMember(tenant.Id, removed);
        _env.Seed(db =>
        {
            db.Tenants.Single(t => t.Id == tenant.Id).LastEventSequence = 7;
            var gone = db.Members.IgnoreQueryFilters().Single(m => m.TenantId == tenant.Id && m.UserId == removed);
            gone.RemovedAt = _env.UtcNow;
            gone.MembershipGeneration = 2;
        });

        var read = await new TenantProjectionSnapshotReader(_env.NewContext()).ReadAsync(tenant.Id);

        Assert.Equal(TenantSnapshotReadStatus.Found, read.Status);
        Assert.Equal(7, read.Snapshot!.HeadSequence);
        Assert.Equal("Active", read.Snapshot.Status);
        Assert.Contains(read.Snapshot.Members, m => m.UserId == active && m.IsActive);
        Assert.Contains(read.Snapshot.Members, m => m.UserId == removed && !m.IsActive && m.MembershipGeneration == 2);
    }

    [Fact]
    public async Task AMissingTenant_IsReportedAsNotFound()
    {
        var read = await new TenantProjectionSnapshotReader(_env.NewContext()).ReadAsync(Guid.NewGuid());

        Assert.Equal(TenantSnapshotReadStatus.TenantNotFound, read.Status);
    }

    [Fact]
    public void TheComparer_SeparatesInFlight_AccessAhead_Drift_AndInSync()
    {
        var user = Guid.NewGuid();
        var snapshot = Snapshot(5, "Active", new TenantProjectionSnapshotMember(user, true, 2));

        TenantProjectionRemoteView View(long seq, string? status, params TenantProjectionSnapshotMember[] m)
            => new(snapshot.TenantId, seq, status, m);

        Assert.Equal(TenantProjectionDriftKind.NotKnownToAccess, TenantProjectionComparer.Compare(snapshot, null).Kind);
        Assert.Equal(TenantProjectionDriftKind.InFlight, TenantProjectionComparer.Compare(snapshot, View(3, "Active")).Kind);
        Assert.Equal(TenantProjectionDriftKind.AccessAhead, TenantProjectionComparer.Compare(snapshot, View(9, "Active")).Kind);
        Assert.Equal(TenantProjectionDriftKind.InSync, TenantProjectionComparer.Compare(
            snapshot, View(5, "Active", new TenantProjectionSnapshotMember(user, true, 2))).Kind);

        var drift = TenantProjectionComparer.Compare(
            snapshot,
            View(5, "Suspended", new TenantProjectionSnapshotMember(user, false, 2), new TenantProjectionSnapshotMember(Guid.NewGuid(), true, 1)));

        Assert.Equal(TenantProjectionDriftKind.Drift, drift.Kind);
        Assert.True(drift.StatusMismatch);
        Assert.Single(drift.ExtraInAccess);
        Assert.Single(drift.MismatchedMembers);
    }

    [Fact]
    public async Task Bootstrap_InstallsEveryTenant_AndReportsTheOnesThatFail()
    {
        var a = _env.AddTenant("A Corp", "a-corp");
        var b = _env.AddTenant("B Corp", "b-corp");
        _gateway.InstallOutcome = id => id == b.Id ? TenantProjectionInstallOutcome.RetryLater : TenantProjectionInstallOutcome.Installed;

        var report = ResultAssert.Ok(await Service().RebuildAllAsync());

        Assert.Equal(2, report.Tenants);
        Assert.Equal(1, report.Installed);
        Assert.Equal(new[] { b.Id }, report.Failed);
        Assert.Contains(_gateway.Installed, s => s.TenantId == a.Id);
    }

    [Fact]
    public async Task Reconcile_RepairsOnlyEqualSequenceDrift_NeverInFlightOrAccessAhead()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        _env.Seed(db => db.Tenants.Single(t => t.Id == tenant.Id).LastEventSequence = 5);
        var service = Service();

        _gateway.View = new TenantProjectionRemoteView(
            tenant.Id, 5, "Suspended", Array.Empty<TenantProjectionSnapshotMember>());
        var drift = ResultAssert.Ok(await service.ReconcileTenantAsync(tenant.Id, repair: true));

        Assert.Equal(TenantProjectionDriftKind.Drift, drift.Kind);
        Assert.True(drift.Repaired);
        Assert.Single(_gateway.Installed);

        _gateway.Installed.Clear();
        _gateway.View = new TenantProjectionRemoteView(
            tenant.Id, 3, "Active", Array.Empty<TenantProjectionSnapshotMember>());
        Assert.Equal(
            TenantProjectionDriftKind.InFlight,
            ResultAssert.Ok(await service.ReconcileTenantAsync(tenant.Id, true)).Kind);

        _gateway.View = new TenantProjectionRemoteView(
            tenant.Id, 9, "Active", Array.Empty<TenantProjectionSnapshotMember>());
        var ahead = ResultAssert.Ok(await service.ReconcileTenantAsync(tenant.Id, true));

        Assert.Equal(TenantProjectionDriftKind.AccessAhead, ahead.Kind);
        Assert.False(ahead.Repaired);

        Assert.Empty(_gateway.Installed);
    }

    [Fact]
    public async Task ReconcileAll_ReportsTenantsAccessKnowsButTenantDoesNot_WithoutTouchingThem()
    {
        _env.AddTenant("Acme", "acme");
        var orphan = Guid.NewGuid();
        _gateway.KnownTenantIds = new[] { orphan };
        _gateway.View = null;

        var summary = ResultAssert.Ok(await Service().ReconcileAllAsync(repair: false));

        Assert.Equal(new[] { orphan }, summary.OrphanedInAccess);
        Assert.Empty(_gateway.Installed);
    }

    [Fact]
    public async Task Requeue_MakesDeadLettersDeliverableAgain_AndOnlyForThatTenant()
    {
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();

        TenantOutboxMessage Message(Guid t, long seq) => new()
        {
            EventId = Guid.NewGuid(),
            TenantId = t,
            Sequence = seq,
            EventType = TenantOutboxEventTypes.MemberAdded,
            SchemaVersion = 1,
            PayloadJson = "{}",
            OccurredAt = _env.UtcNow,
            CreatedAt = _env.UtcNow,
            NextAttemptAt = _env.UtcNow.AddHours(1),
            Status = TenantOutboxStatus.DeadLettered,
            AttemptCount = 10,
            DeadLetteredAt = _env.UtcNow
        };

        _env.Seed(db =>
        {
            db.OutboxMessages.Add(Message(tenant, 1));
            db.OutboxMessages.Add(Message(other, 1));
        });

        var count = ResultAssert.Ok(await Service().RequeueDeadLettersAsync(tenant));

        Assert.Equal(1, count);

        var rows = _env.Read(db => db.OutboxMessages.AsNoTracking().ToList());
        var mine = rows.Single(r => r.TenantId == tenant);

        Assert.Equal(TenantOutboxStatus.Pending, mine.Status);
        Assert.Equal(0, mine.AttemptCount);
        Assert.Null(mine.DeadLetteredAt);
        Assert.Equal(TenantOutboxStatus.DeadLettered, rows.Single(r => r.TenantId == other).Status);
    }

    private sealed class FakeGateway : ITenantProjectionGateway
    {
        public List<TenantProjectionSnapshot> Installed { get; } = new();

        public Func<Guid, TenantProjectionInstallOutcome> InstallOutcome { get; set; }
            = _ => TenantProjectionInstallOutcome.Installed;

        public TenantProjectionRemoteView? View { get; set; }

        public IReadOnlyList<Guid> KnownTenantIds { get; set; } = Array.Empty<Guid>();

        public Task<TenantProjectionInstallOutcome> InstallAsync(TenantProjectionSnapshot snapshot, CancellationToken ct)
        {
            Installed.Add(snapshot);
            return Task.FromResult(InstallOutcome(snapshot.TenantId));
        }

        public Task<TenantProjectionRemoteView?> GetViewAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(View);

        public Task<IReadOnlyList<Guid>> ListKnownTenantIdsAsync(CancellationToken ct) => Task.FromResult(KnownTenantIds);
    }
}