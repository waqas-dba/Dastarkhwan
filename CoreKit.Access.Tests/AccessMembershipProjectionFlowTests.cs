using System.Text.Json;

namespace CoreKit.Access.Tests;

/// <summary>
/// What this protects: the Access side of the tenant membership contract — the durable consumer, the snapshot
/// installer, the freshness gate and the projection adapter. Access must never move backwards, must never skip
/// a sequence, must never resurrect a revoked member, and must fail closed whenever it cannot trust the
/// projection.
/// </summary>
public sealed class AccessMembershipEventConsumerTests : IDisposable
{
    private readonly TestEnv _env = new();
    private readonly Graph _graph;

    public AccessMembershipEventConsumerTests() => _graph = new Graph(_env);

    public void Dispose()
    {
        _graph.Dispose();
        _env.Dispose();
    }

    [Fact]
    public async Task MemberAdded_AppliesProjectionCursorAndProcessedEvent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var envelope = ProjectionTests.MemberAdded(tenantId, 1, userId, 1);

        var result = await _graph.Consumer.ApplyAsync(envelope);

        Assert.Equal(AccessMembershipEventOutcome.Applied, result.Outcome);

        var row = await _graph.Projections.FindReadOnlyAsync(tenantId, userId);
        Assert.NotNull(row);
        Assert.Equal(AccessMembershipProjectionStatus.Active, row!.Status);
        Assert.Equal(1, row.MembershipGeneration);
        Assert.Equal(1, row.LastAppliedSequence);

        var cursor = await _graph.Stream.FindCursorAsync(tenantId);
        Assert.Equal(1, cursor!.LastAppliedSequence);

        var processed = await _graph.Stream.FindProcessedAsync(envelope.EventId);
        Assert.NotNull(processed);
    }

    [Fact]
    public async Task RedeliveryOfTheSameEvent_IsDuplicate()
    {
        var tenantId = Guid.NewGuid();
        var envelope = ProjectionTests.MemberAdded(tenantId, 1, Guid.NewGuid(), 1);

        await _graph.Consumer.ApplyAsync(envelope);
        var again = await _graph.Consumer.ApplyAsync(envelope);

        Assert.Equal(AccessMembershipEventOutcome.Duplicate, again.Outcome);
        Assert.Single(await _graph.Projections.ListByTenantAsync(tenantId));
        Assert.Equal(1, (await _graph.Stream.FindCursorAsync(tenantId))!.LastAppliedSequence);
    }

    [Fact]
    public async Task ASequenceGap_IsRetriedNotSkipped()
    {
        var tenantId = Guid.NewGuid();

        var result = await _graph.Consumer.ApplyAsync(ProjectionTests.MemberAdded(tenantId, 2, Guid.NewGuid(), 1));

        Assert.Equal(AccessMembershipEventOutcome.RetryLater, result.Outcome);
        Assert.Null(await _graph.Stream.FindCursorAsync(tenantId));
        Assert.Empty(await _graph.Projections.ListByTenantAsync(tenantId));
    }

    [Fact]
    public async Task AStaleGeneration_IsRejectedAndLeavesTheProjectionAlone()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _graph.Consumer.ApplyAsync(ProjectionTests.MemberAdded(tenantId, 1, userId, 5));
        var stale = await _graph.Consumer.ApplyAsync(
            ProjectionTests.MemberRemoved(tenantId, 2, userId, 3));

        Assert.Equal(AccessMembershipEventOutcome.Rejected, stale.Outcome);

        var row = await _graph.Projections.FindReadOnlyAsync(tenantId, userId);
        Assert.Equal(AccessMembershipProjectionStatus.Active, row!.Status);
        Assert.Equal(5, row.MembershipGeneration);
    }

    [Fact]
    public async Task MemberRemoved_AfterAdd_MarksRemoved()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _graph.Consumer.ApplyAsync(ProjectionTests.MemberAdded(tenantId, 1, userId, 1));
        await _graph.Consumer.ApplyAsync(ProjectionTests.MemberRemoved(tenantId, 2, userId, 2));

        var row = await _graph.Projections.FindReadOnlyAsync(tenantId, userId);
        Assert.Equal(AccessMembershipProjectionStatus.Removed, row!.Status);
        Assert.Equal(2, row.MembershipGeneration);
    }

    [Fact]
    public async Task ReactivationAfterRemoval_RestoresTheMember()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _graph.Consumer.ApplyAsync(ProjectionTests.MemberAdded(tenantId, 1, userId, 1));
        await _graph.Consumer.ApplyAsync(ProjectionTests.MemberRemoved(tenantId, 2, userId, 2));
        await _graph.Consumer.ApplyAsync(ProjectionTests.MemberReactivated(tenantId, 3, userId, 3));

        var row = await _graph.Projections.FindReadOnlyAsync(tenantId, userId);
        Assert.Equal(AccessMembershipProjectionStatus.Active, row!.Status);
        Assert.Equal(3, row.MembershipGeneration);
    }

    [Fact]
    public async Task TenantCreatedThenStatusChanged_UpdatesTheLifecycleState()
    {
        var tenantId = Guid.NewGuid();

        await _graph.Consumer.ApplyAsync(ProjectionTests.TenantCreated(tenantId, 1, AccessTenantStatuses.Active));
        await _graph.Consumer.ApplyAsync(
            ProjectionTests.TenantStatusChanged(tenantId, 2, AccessTenantStatuses.Active, AccessTenantStatuses.Suspended));

        Assert.Equal(AccessTenantStatuses.Suspended, await _graph.TenantStates.GetStatusAsync(tenantId));
    }

    [Fact]
    public async Task TenantDeleted_IsTerminal()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _graph.Consumer.ApplyAsync(ProjectionTests.TenantCreated(tenantId, 1, AccessTenantStatuses.Active));
        await _graph.Consumer.ApplyAsync(ProjectionTests.TenantDeleted(tenantId, 2));

        var afterDelete = await _graph.Consumer.ApplyAsync(
            ProjectionTests.MemberAdded(tenantId, 3, userId, 1));

        Assert.Equal(AccessMembershipEventOutcome.Rejected, afterDelete.Outcome);
        Assert.Equal(AccessTenantStatuses.Deleted, await _graph.TenantStates.GetStatusAsync(tenantId));
    }

    [Fact]
    public async Task UnsupportedSchemaVersion_IsRejected()
    {
        var tenantId = Guid.NewGuid();

        var result = await _graph.Consumer.ApplyAsync(
            ProjectionTests.MemberAdded(tenantId, 1, Guid.NewGuid(), 1, schemaVersion: 2));

        Assert.Equal(AccessMembershipEventOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task PayloadThatDisagreesWithTheEnvelope_IsRejected()
    {
        var tenantId = Guid.NewGuid();

        var result = await _graph.Consumer.ApplyAsync(
            ProjectionTests.MemberAdded(tenantId, 1, Guid.NewGuid(), 1, payloadTenantId: Guid.NewGuid()));

        Assert.Equal(AccessMembershipEventOutcome.Rejected, result.Outcome);
    }
}

public sealed class AccessMembershipSnapshotInstallerTests : IDisposable
{
    private readonly TestEnv _env = new();
    private readonly Graph _graph;

    public AccessMembershipSnapshotInstallerTests() => _graph = new Graph(_env);

    public void Dispose()
    {
        _graph.Dispose();
        _env.Dispose();
    }

    [Fact]
    public async Task Install_CreatesRowsStateAndCursor()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var outcome = await _graph.Installer.InstallAsync(ProjectionTests.Snapshot(
            tenantId, AccessTenantStatuses.Active, 5,
            new AccessMembershipSnapshotMember(userA, true, 1),
            new AccessMembershipSnapshotMember(userB, false, 2)));

        Assert.Equal(AccessSnapshotOutcome.Installed, outcome);

        var rows = await _graph.Projections.ListByTenantAsync(tenantId);
        Assert.Equal(2, rows.Count);
        Assert.Equal(AccessMembershipProjectionStatus.Removed, rows.Single(r => r.UserId == userB).Status);

        Assert.Equal(5, (await _graph.Stream.FindCursorAsync(tenantId))!.LastAppliedSequence);
        Assert.Equal(AccessTenantStatuses.Active, await _graph.TenantStates.GetStatusAsync(tenantId));
    }

    [Fact]
    public async Task Install_IgnoresASnapshotOlderThanTheCursor()
    {
        var tenantId = Guid.NewGuid();

        await _graph.Installer.InstallAsync(ProjectionTests.Snapshot(
            tenantId, AccessTenantStatuses.Active, 5, new AccessMembershipSnapshotMember(Guid.NewGuid(), true, 1)));

        var older = await _graph.Installer.InstallAsync(ProjectionTests.Snapshot(
            tenantId, AccessTenantStatuses.Active, 4, new AccessMembershipSnapshotMember(Guid.NewGuid(), true, 1)));

        Assert.Equal(AccessSnapshotOutcome.IgnoredStale, older);
        Assert.Equal(5, (await _graph.Stream.FindCursorAsync(tenantId))!.LastAppliedSequence);
    }

    [Fact]
    public async Task Install_SameSequenceDifferentContent_Repairs()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();

        await _graph.Installer.InstallAsync(ProjectionTests.Snapshot(
            tenantId, AccessTenantStatuses.Active, 2, new AccessMembershipSnapshotMember(userA, true, 1)));

        var repaired = await _graph.Installer.InstallAsync(ProjectionTests.Snapshot(
            tenantId, AccessTenantStatuses.Active, 2,
            new AccessMembershipSnapshotMember(userA, false, 2),
            new AccessMembershipSnapshotMember(Guid.NewGuid(), true, 1)));

        Assert.Equal(AccessSnapshotOutcome.Repaired, repaired);
        Assert.Equal(AccessMembershipProjectionStatus.Removed,
            (await _graph.Projections.FindReadOnlyAsync(tenantId, userA))!.Status);
    }

    [Fact]
    public async Task Install_SameSequenceSameContent_IsUnchanged()
    {
        var tenantId = Guid.NewGuid();
        var snapshot = ProjectionTests.Snapshot(
            tenantId, AccessTenantStatuses.Active, 2, new AccessMembershipSnapshotMember(Guid.NewGuid(), true, 1));

        await _graph.Installer.InstallAsync(snapshot);
        var again = await _graph.Installer.InstallAsync(snapshot);

        Assert.Equal(AccessSnapshotOutcome.Unchanged, again);
    }

    [Fact]
    public async Task Install_DropsMembersMissingFromTheSnapshot()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await _graph.Installer.InstallAsync(ProjectionTests.Snapshot(
            tenantId, AccessTenantStatuses.Active, 1,
            new AccessMembershipSnapshotMember(userA, true, 1),
            new AccessMembershipSnapshotMember(userB, true, 1)));

        await _graph.Installer.InstallAsync(ProjectionTests.Snapshot(
            tenantId, AccessTenantStatuses.Active, 2, new AccessMembershipSnapshotMember(userA, true, 1)));

        var rows = await _graph.Projections.ListByTenantAsync(tenantId);
        Assert.Single(rows);
        Assert.Equal(userA, rows[0].UserId);
    }

    [Fact]
    public async Task Install_AfterTheTenantWasDeleted_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        _env.Seed(db => db.TenantStates.Add(new AccessTenantState
        {
            TenantId = tenantId, Status = AccessTenantStatuses.Deleted, UpdatedAt = _env.UtcNow
        }));

        var outcome = await _graph.Installer.InstallAsync(ProjectionTests.Snapshot(
            tenantId, AccessTenantStatuses.Active, 1, new AccessMembershipSnapshotMember(Guid.NewGuid(), true, 1)));

        Assert.Equal(AccessSnapshotOutcome.Rejected, outcome);
    }

    [Fact]
    public async Task Install_AnInvalidSnapshot_IsRejected()
    {
        var outcome = await _graph.Installer.InstallAsync(new AccessMembershipSnapshot(
            Guid.NewGuid(), "Nonsense", 1,
            new[] { new AccessMembershipSnapshotMember(Guid.NewGuid(), true, 1) }));

        Assert.Equal(AccessSnapshotOutcome.Rejected, outcome);
    }
}

public sealed class AccessMembershipFreshnessRecorderTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task Heartbeat_CreatesTheWatermark()
    {
        var now = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
        using var graph = new Graph(_env, new FixedTimeProvider(now));

        await graph.Recorder.HeartbeatAsync();

        var watermark = await graph.Freshness.GetWatermarkAsync();
        Assert.NotNull(watermark);
        Assert.Equal(Guid.Empty, watermark!.Id);
        Assert.Equal(now.UtcDateTime, watermark.AsOf);
    }

    [Fact]
    public async Task Heartbeat_NeverMovesTheWatermarkBackwards()
    {
        var now = new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

        using (var graph = new Graph(_env, new FixedTimeProvider(now)))
            await graph.Recorder.HeartbeatAsync();

        using (var older = new Graph(_env, new FixedTimeProvider(now.AddMinutes(-10))))
            await older.Recorder.HeartbeatAsync();

        using (var reader = new Graph(_env, new FixedTimeProvider(now)))
            Assert.Equal(now.UtcDateTime, (await reader.Freshness.GetWatermarkAsync())!.AsOf);
    }

    [Fact]
    public async Task Quarantine_ThenClear()
    {
        var tenantId = Guid.NewGuid();
        using var graph = new Graph(_env);

        await graph.Recorder.QuarantineAsync(tenantId);
        Assert.True(await graph.Freshness.IsQuarantinedAsync(tenantId));

        await graph.Recorder.ClearQuarantineAsync(tenantId);
        Assert.False(await graph.Freshness.IsQuarantinedAsync(tenantId));
    }
}

public sealed class ProjectionAccessMembershipTests : IDisposable
{
    private readonly TestEnv _env = new();
    private readonly Graph _graph;

    public ProjectionAccessMembershipTests()
    {
        _graph = new Graph(_env, options: new AccessOptions
        {
            MembershipSource = AccessMembershipSource.Projection,
            MaxMembershipStalenessSeconds = 30
        });
    }

    public void Dispose()
    {
        _graph.Dispose();
        _env.Dispose();
    }

    [Fact]
    public async Task TenantAdapterMode_NeverReadsTheProjection()
    {
        using var legacy = new Graph(_env, options: new AccessOptions());

        Assert.Null(await legacy.Projection.FindAsync(
            AccessScope.ForTenant(Guid.NewGuid()), Guid.NewGuid()));
    }

    [Fact]
    public async Task NoWatermark_Denies()
    {
        Assert.Null(await _graph.Projection.FindAsync(
            AccessScope.ForTenant(Guid.NewGuid()), Guid.NewGuid()));
    }

    [Fact]
    public async Task StaleWatermark_Denies()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedTrustedTenant(tenantId, userId, watermarkAge: TimeSpan.FromMinutes(5));

        Assert.Null(await _graph.Projection.FindAsync(AccessScope.ForTenant(tenantId), userId));
    }

    [Fact]
    public async Task QuarantinedTenant_Denies()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedTrustedTenant(tenantId, userId, watermarkAge: TimeSpan.Zero);
        _env.Seed(db => db.QuarantinedTenants.Add(new AccessQuarantinedTenant
        {
            TenantId = tenantId, Since = _env.UtcNow
        }));

        Assert.Null(await _graph.Projection.FindAsync(AccessScope.ForTenant(tenantId), userId));
    }

    [Fact]
    public async Task UnknownTenantState_Denies()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedWatermark(watermarkAge: TimeSpan.Zero);
        _env.Seed(db => db.MembershipProjections.Add(new AccessMembershipProjection
        {
            TenantId = tenantId,
            UserId = userId,
            Status = AccessMembershipProjectionStatus.Active,
            MembershipGeneration = 1,
            LastAppliedSequence = 1,
            UpdatedAt = _env.UtcNow
        }));

        Assert.Null(await _graph.Projection.FindAsync(AccessScope.ForTenant(tenantId), userId));
    }

    [Fact]
    public async Task NonActiveTenant_Denies()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedWatermark(watermarkAge: TimeSpan.Zero);
        _env.Seed(db =>
        {
            db.TenantStates.Add(new AccessTenantState
            {
                TenantId = tenantId, Status = AccessTenantStatuses.Suspended, UpdatedAt = _env.UtcNow
            });
            db.MembershipProjections.Add(new AccessMembershipProjection
            {
                TenantId = tenantId,
                UserId = userId,
                Status = AccessMembershipProjectionStatus.Active,
                MembershipGeneration = 1,
                LastAppliedSequence = 1,
                UpdatedAt = _env.UtcNow
            });
        });

        Assert.Null(await _graph.Projection.FindAsync(AccessScope.ForTenant(tenantId), userId));
    }

    [Fact]
    public async Task ActiveMember_IsReturnedWithoutOwner()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedTrustedTenant(tenantId, userId, watermarkAge: TimeSpan.Zero);

        var member = await _graph.Projection.FindAsync(AccessScope.ForTenant(tenantId), userId);

        Assert.NotNull(member);
        Assert.False(member!.IsOwner);
        Assert.Equal(1, member.MembershipGeneration);
    }

    [Fact]
    public async Task PlatformScope_IsNotAProjectionLookup()
    {
        Assert.Null(await _graph.Projection.FindAsync(
            AccessScope.ForPlatform(AccessScope.PlatformId), Guid.NewGuid()));
    }

    private async Task SeedTrustedTenant(Guid tenantId, Guid userId, TimeSpan watermarkAge)
    {
        await SeedWatermark(watermarkAge);
        _env.Seed(db =>
        {
            db.TenantStates.Add(new AccessTenantState
            {
                TenantId = tenantId, Status = AccessTenantStatuses.Active, UpdatedAt = _env.UtcNow
            });
            db.MembershipProjections.Add(new AccessMembershipProjection
            {
                TenantId = tenantId,
                UserId = userId,
                Status = AccessMembershipProjectionStatus.Active,
                MembershipGeneration = 1,
                LastAppliedSequence = 1,
                UpdatedAt = _env.UtcNow
            });
        });
    }

    private async Task SeedWatermark(TimeSpan watermarkAge)
    {
        await _graph.Recorder.HeartbeatAsync();

        if (watermarkAge <= TimeSpan.Zero)
            return;

        var watermark = await _graph.Freshness.GetWatermarkAsync();
        Assert.NotNull(watermark);

        _env.Seed(db =>
        {
            var row = db.MembershipWatermarks.Single();
            row.AsOf = _env.Time.GetUtcNow().UtcDateTime - watermarkAge;
        });
    }
}

public sealed class AccessMembershipProjectionReaderTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task NoRows_ReturnsNull()
    {
        using var graph = new Graph(_env);

        Assert.Null(await graph.Reader.GetTenantViewAsync(Guid.NewGuid()));
        Assert.Empty(await graph.Reader.ListTenantIdsAsync());
    }

    [Fact]
    public async Task ReturnsSequenceStatusAndMembers()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _env.Seed(db =>
        {
            db.MembershipStreamCursors.Add(new AccessMembershipStreamCursor
            {
                TenantId = tenantId, LastAppliedSequence = 7, UpdatedAt = _env.UtcNow
            });
            db.TenantStates.Add(new AccessTenantState
            {
                TenantId = tenantId, Status = AccessTenantStatuses.Active, UpdatedAt = _env.UtcNow
            });
            db.MembershipProjections.Add(new AccessMembershipProjection
            {
                TenantId = tenantId,
                UserId = userId,
                Status = AccessMembershipProjectionStatus.Active,
                MembershipGeneration = 3,
                LastAppliedSequence = 7,
                UpdatedAt = _env.UtcNow
            });
        });

        using var graph = new Graph(_env);

        var view = await graph.Reader.GetTenantViewAsync(tenantId);

        Assert.NotNull(view);
        Assert.Equal(7, view!.LastAppliedSequence);
        Assert.Equal(AccessTenantStatuses.Active, view.TenantStatus);
        var member = Assert.Single(view.Members);
        Assert.Equal(userId, member.UserId);
        Assert.True(member.IsActive);
        Assert.Equal(3, member.MembershipGeneration);
    }

    [Fact]
    public async Task ListTenantIds_UnionsEverySource()
    {
        var tenantId = Guid.NewGuid();

        _env.Seed(db => db.MembershipStreamCursors.Add(new AccessMembershipStreamCursor
        {
            TenantId = tenantId, LastAppliedSequence = 1, UpdatedAt = _env.UtcNow
        }));

        using var graph = new Graph(_env);

        Assert.Contains(tenantId, await graph.Reader.ListTenantIdsAsync());
    }
}

/// <summary>
/// One real object graph over the test's in-memory database. Kept separate from <see cref="TestEnv"/> so a
/// test can build more than one graph (and more than one clock) over the same data.
/// </summary>
internal sealed class Graph : IDisposable
{
    private readonly AccessDbContext _db;

    public Graph(TestEnv env, FixedTimeProvider? time = null, AccessOptions? options = null)
    {
        _db = env.NewContext();
        Time = time ?? env.Time;
        Options = Microsoft.Extensions.Options.Options.Create(options ?? new AccessOptions());

        Stream = new AccessMembershipStreamRepository(_db);
        Projections = new AccessMembershipProjectionRepository(_db);
        TenantStates = new AccessTenantStateRepository(_db);
        Freshness = new AccessMembershipFreshnessRepository(_db);
        UnitOfWork = new AccessUnitOfWork(_db, NullLogger<AccessUnitOfWork>.Instance);

        Consumer = new AccessMembershipEventConsumer(
            Stream, Projections, TenantStates, UnitOfWork, Time, NullLogger<AccessMembershipEventConsumer>.Instance);
        Installer = new AccessMembershipSnapshotInstaller(
            Stream, Projections, TenantStates, UnitOfWork, Time);
        Recorder = new AccessMembershipFreshnessRecorder(
            Freshness, UnitOfWork, Time, NullLogger<AccessMembershipFreshnessRecorder>.Instance);
        Reader = new AccessMembershipProjectionReader(Stream, Projections, TenantStates);
        Projection = new ProjectionAccessMembership(
            (IAccessMembershipFreshnessRepository)Freshness, Projections, TenantStates, Options, Time);
    }

    public FixedTimeProvider Time { get; }

    public IOptions<AccessOptions> Options { get; }

    public AccessMembershipStreamRepository Stream { get; }

    public AccessMembershipProjectionRepository Projections { get; }

    public AccessTenantStateRepository TenantStates { get; }

    public AccessMembershipFreshnessRepository Freshness { get; }

    public AccessUnitOfWork UnitOfWork { get; }

    public AccessMembershipEventConsumer Consumer { get; }

    public AccessMembershipSnapshotInstaller Installer { get; }

    public AccessMembershipFreshnessRecorder Recorder { get; }

    public AccessMembershipProjectionReader Reader { get; }

    public ProjectionAccessMembership Projection { get; }

    public void Dispose() => _db.Dispose();
}

internal static class ProjectionTests
{
    public static AccessMembershipEventEnvelope MemberAdded(
        Guid tenantId, long sequence, Guid userId, long generation,
        int schemaVersion = AccessMembershipEventTypes.SupportedSchemaVersion,
        Guid? payloadTenantId = null)
        => MemberEvent(
            tenantId, sequence, AccessMembershipEventTypes.MemberAdded, userId, generation, true,
            schemaVersion, payloadTenantId);

    public static AccessMembershipEventEnvelope MemberReactivated(
        Guid tenantId, long sequence, Guid userId, long generation)
        => MemberEvent(
            tenantId, sequence, AccessMembershipEventTypes.MemberReactivated, userId, generation, true,
            AccessMembershipEventTypes.SupportedSchemaVersion, null);

    public static AccessMembershipEventEnvelope MemberRemoved(
        Guid tenantId, long sequence, Guid userId, long generation)
        => MemberEvent(
            tenantId, sequence, AccessMembershipEventTypes.MemberRemoved, userId, generation, false,
            AccessMembershipEventTypes.SupportedSchemaVersion, null);

    public static AccessMembershipEventEnvelope TenantCreated(Guid tenantId, long sequence, string status)
        => LifecycleEvent(tenantId, sequence, AccessMembershipEventTypes.TenantCreated, null, status);

    public static AccessMembershipEventEnvelope TenantStatusChanged(
        Guid tenantId, long sequence, string from, string to)
        => LifecycleEvent(tenantId, sequence, AccessMembershipEventTypes.TenantStatusChanged, from, to);

    public static AccessMembershipEventEnvelope TenantDeleted(Guid tenantId, long sequence)
        => LifecycleEvent(tenantId, sequence, AccessMembershipEventTypes.TenantDeleted, null, AccessTenantStatuses.Deleted);

    public static AccessMembershipSnapshot Snapshot(
        Guid tenantId, string status, long head, params AccessMembershipSnapshotMember[] members)
        => new(tenantId, status, head, members);

    private static AccessMembershipEventEnvelope MemberEvent(
        Guid tenantId, long sequence, string type, Guid userId, long generation, bool isActive,
        int schemaVersion, Guid? payloadTenantId)
    {
        var occurredAt = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var payload = JsonSerializer.Serialize(new
        {
            tenantId = payloadTenantId ?? tenantId,
            userId,
            membershipGeneration = generation,
            isOwner = false,
            isActive,
            occurredAt
        });

        return new AccessMembershipEventEnvelope(
            Guid.NewGuid(), tenantId, sequence, type, schemaVersion, userId, generation, payload, occurredAt);
    }

    private static AccessMembershipEventEnvelope LifecycleEvent(
        Guid tenantId, long sequence, string type, string? fromStatus, string status)
    {
        var occurredAt = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var payload = JsonSerializer.Serialize(new { tenantId, fromStatus, status, occurredAt });

        return new AccessMembershipEventEnvelope(
            Guid.NewGuid(), tenantId, sequence, type, AccessMembershipEventTypes.SupportedSchemaVersion,
            null, null, payload, occurredAt);
    }
}