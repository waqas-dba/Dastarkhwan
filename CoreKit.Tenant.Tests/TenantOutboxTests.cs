using System.Text.Json;
using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Events;
using CoreKit.Tenant.Persistence;
using CoreKit.Tenant.Services;
using CoreKit.Tenant.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoreKit.Tenant.Tests;

public sealed class TenantOutboxTests : IDisposable
{
    private readonly TestEnv _env = new();
    private readonly TenantEntity _tenant;

    public TenantOutboxTests() => _tenant = _env.AddTenant("Acme", "acme");

    public void Dispose() => _env.Dispose();

    private List<TenantOutboxMessage> Outbox() =>
        _env.Read(db => db.OutboxMessages.AsNoTracking().OrderBy(m => m.Sequence).ToList());

    [Fact]
    public async Task AddMember_WritesOneOutboxMessage_InTheSameCommit()
    {
        var userId = Guid.NewGuid();

        ResultAssert.Ok(await _env.CreateMembershipService()
            .AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = userId }));

        var message = Assert.Single(Outbox());
        Assert.Equal(TenantOutboxEventTypes.MemberAdded, message.EventType);
        Assert.Equal(1, message.Sequence);
        Assert.Equal(1, message.MembershipGeneration);
        Assert.Equal(userId, message.SubjectUserId);
        Assert.Equal(TenantOutboxStatus.Pending, message.Status);
        Assert.Equal(TenantOutboxEventTypes.CurrentSchemaVersion, message.SchemaVersion);
        Assert.Equal(1, _env.Read(db => db.Tenants.Single(t => t.Id == _tenant.Id).LastEventSequence));

        var payload = JsonSerializer.Deserialize<TenantMembershipEventPayload>(message.PayloadJson)!;
        Assert.Equal(_tenant.Id, payload.TenantId);
        Assert.Equal(userId, payload.UserId);
        Assert.True(payload.IsActive);
    }

    [Fact]
    public async Task AddRemoveReAdd_ProducesGapFreeSequences_AndNeverMovesGenerationBackwards()
    {
        var userId = Guid.NewGuid();

        ResultAssert.Ok(await _env.CreateMembershipService()
            .AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = userId }));
        ResultAssert.Ok(await _env.CreateMembershipService().RemoveMemberAsync(_tenant.Id, userId));
        ResultAssert.Ok(await _env.CreateMembershipService()
            .AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = userId }));

        var messages = Outbox();
        Assert.Equal(new long[] { 1, 2, 3 }, messages.Select(m => m.Sequence).ToArray());
        Assert.Equal(
            new[]
            {
                TenantOutboxEventTypes.MemberAdded,
                TenantOutboxEventTypes.MemberRemoved,
                TenantOutboxEventTypes.MemberReactivated
            },
            messages.Select(m => m.EventType).ToArray());
        Assert.Equal(new long?[] { 1, 2, 3 }, messages.Select(m => m.MembershipGeneration).ToArray());
        Assert.False(JsonSerializer.Deserialize<TenantMembershipEventPayload>(messages[1].PayloadJson)!.IsActive);
        Assert.Equal(3, _env.Read(db => db.Tenants.Single(t => t.Id == _tenant.Id).LastEventSequence));
    }

    [Fact]
    public async Task SetOwner_WritesOwnershipEvent_WithoutChangingGeneration()
    {
        var userId = Guid.NewGuid();
        _env.Actor.UserId = Guid.NewGuid();
        _env.AddMember(_tenant.Id, userId);

        ResultAssert.Ok(await _env.CreateMembershipService()
            .SetOwnerAsync(_tenant.Id, userId, new SetTenantOwnerRequest { IsOwner = true }));

        var message = Assert.Single(Outbox());
        Assert.Equal(TenantOutboxEventTypes.MemberOwnershipChanged, message.EventType);
        Assert.Equal(1, message.MembershipGeneration);
        Assert.True(JsonSerializer.Deserialize<TenantMembershipEventPayload>(message.PayloadJson)!.IsOwner);
    }

    [Fact]
    public async Task FailedTransaction_LeavesNoOrphanEvent_NoMember_AndNoConsumedSequence()
    {
        var userId = Guid.NewGuid();
        var db = _env.NewContext();
        var racing = new RacingUnitOfWork(
            new TenantUnitOfWork(db, NullLogger<TenantUnitOfWork>.Instance),
            () => _env.Seed(s => s.Tenants.Single(t => t.Id == _tenant.Id).ConcurrencyStamp = Guid.NewGuid()));

        var service = new TenantMembershipService(
            new TenantRepository(db), new TenantMemberRepository(db),
            new TenantAuditRecorder(new TenantAuditRepository(db), _env.Actor, _env.Time),
            racing, _env.Publisher, _env.Guard, _env.PlatformAccess, _env.Actor, _env.Permissions, _env.Time,
            _env.MembershipReader);

        ResultAssert.Fails(
            await service.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = userId }),
            TenantErrorKind.Conflict);

        Assert.Empty(Outbox());
        Assert.Empty(_env.Read(c => c.Members.IgnoreQueryFilters().Where(m => m.UserId == userId).ToList()));
        Assert.Equal(0, _env.Read(c => c.Tenants.Single(t => t.Id == _tenant.Id).LastEventSequence));
    }

    [Fact]
    public void Stage_RefusesAnUntrackedTenant()
    {
        var detached = new TenantEntity { Id = Guid.NewGuid(), Name = "X", Slug = "x", ConcurrencyStamp = Guid.NewGuid() };
        var member = new TenantMember { TenantId = detached.Id, UserId = Guid.NewGuid(), MembershipGeneration = 1 };

        Assert.Throws<InvalidOperationException>(() =>
            new TenantOutbox(_env.Db).StageMembershipEvent(
                detached, TenantOutboxEventTypes.MemberAdded, member, _env.UtcNow));
    }

    [Fact]
    public void DuplicateTenantSequence_IsRefusedByTheDatabase()
    {
        TenantOutboxMessage Make() => new()
        {
            EventId = Guid.NewGuid(),
            TenantId = _tenant.Id,
            Sequence = 1,
            EventType = TenantOutboxEventTypes.MemberAdded,
            SchemaVersion = 1,
            PayloadJson = "{}",
            OccurredAt = _env.UtcNow,
            CreatedAt = _env.UtcNow,
            NextAttemptAt = _env.UtcNow
        };

        Assert.Throws<DbUpdateException>(() =>
            _env.Seed(db => { db.OutboxMessages.Add(Make()); db.OutboxMessages.Add(Make()); }));
    }

    private sealed class RacingUnitOfWork : ITenantUnitOfWork
    {
        private readonly ITenantUnitOfWork _inner;
        private readonly Action _beforeSave;

        public RacingUnitOfWork(ITenantUnitOfWork inner, Action beforeSave)
        {
            _inner = inner;
            _beforeSave = beforeSave;
        }

        public Task<SaveOutcome> SaveAsync(CancellationToken ct = default)
        {
            _beforeSave();
            return _inner.SaveAsync(ct);
        }
    }
}