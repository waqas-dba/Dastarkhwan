using System.Text.Json;

namespace CoreKit.Tenant.Tests;

/// <summary>
/// Proves that tenant creation, status changes and deletion all write their outbox events in the same
/// transaction as the change. Tenant creation previously bypassed the outbox, so no tenant's initial owner
/// ever reached the Access projection.
/// </summary>
public sealed class TenantLifecycleOutboxTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private List<TenantOutboxMessage> Outbox(Guid tenantId) => _env.Read(db =>
        db.OutboxMessages.AsNoTracking().Where(m => m.TenantId == tenantId).OrderBy(m => m.Sequence).ToList());

    [Fact]
    public async Task CreatingATenantWithAnOwner_WritesTheCreatedAndOwnerEvents_InOneCommit()
    {
        var owner = Guid.NewGuid();

        var dto = ResultAssert.Ok(await _env.CreateTenantService().CreateAsync(
            new CreateTenantRequest { Name = "Acme Foods", OwnerUserId = owner, ActivateImmediately = true }));

        var messages = Outbox(dto.Id);

        Assert.Equal(
            new[] { TenantOutboxEventTypes.TenantCreated, TenantOutboxEventTypes.MemberAdded },
            messages.Select(m => m.EventType).ToArray());
        Assert.Equal(new long[] { 1, 2 }, messages.Select(m => m.Sequence).ToArray());
        Assert.Equal(owner, messages[1].SubjectUserId);
        Assert.Equal(1, messages[1].MembershipGeneration);

        var created = JsonSerializer.Deserialize<TenantLifecycleEventPayload>(messages[0].PayloadJson)!;
        Assert.Null(created.FromStatus);
        Assert.Equal("Active", created.Status);

        var member = JsonSerializer.Deserialize<TenantMembershipEventPayload>(messages[1].PayloadJson)!;
        Assert.Equal(dto.Id, member.TenantId);
        Assert.Equal(owner, member.UserId);
        Assert.True(member.IsOwner);
        Assert.True(member.IsActive);

        Assert.Equal(2, _env.Read(db => db.Tenants.Single(t => t.Id == dto.Id).LastEventSequence));
    }

    [Fact]
    public async Task CreatingAPendingTenant_RecordsPending_AndTheOwnerEvent()
    {
        var owner = Guid.NewGuid();

        var dto = ResultAssert.Ok(await _env.CreateTenantService().CreateAsync(
            new CreateTenantRequest { Name = "Acme Foods", OwnerUserId = owner, ActivateImmediately = false }));

        var messages = Outbox(dto.Id);
        Assert.Equal(
            new[] { TenantOutboxEventTypes.TenantCreated, TenantOutboxEventTypes.MemberAdded },
            messages.Select(m => m.EventType).ToArray());

        var created = JsonSerializer.Deserialize<TenantLifecycleEventPayload>(messages[0].PayloadJson)!;
        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task StatusChanges_AreSequencedAfterTheMembershipEvents()
    {
        var dto = ResultAssert.Ok(await _env.CreateTenantService().CreateAsync(
            new CreateTenantRequest { Name = "Acme Foods", OwnerUserId = Guid.NewGuid(), ActivateImmediately = true }));

        ResultAssert.Ok(await _env.CreateTenantService().SuspendAsync(
            dto.Id, new ChangeTenantStatusRequest { Reason = "billing" }));
        ResultAssert.Ok(await _env.CreateTenantService().ActivateAsync(dto.Id));

        var changes = Outbox(dto.Id).Where(m => m.EventType == TenantOutboxEventTypes.TenantStatusChanged).ToList();

        Assert.Equal(new long[] { 3, 4 }, changes.Select(m => m.Sequence).ToArray());

        var suspend = JsonSerializer.Deserialize<TenantLifecycleEventPayload>(changes[0].PayloadJson)!;
        Assert.Equal("Active", suspend.FromStatus);
        Assert.Equal("Suspended", suspend.Status);

        var restore = JsonSerializer.Deserialize<TenantLifecycleEventPayload>(changes[1].PayloadJson)!;
        Assert.Equal("Suspended", restore.FromStatus);
        Assert.Equal("Active", restore.Status);
    }

    [Fact]
    public async Task DeletingAnArchivedTenant_LeavesADeletedEventThatOutlivesTheTenant()
    {
        var dto = ResultAssert.Ok(await _env.CreateTenantService().CreateAsync(
            new CreateTenantRequest { Name = "Acme Foods", OwnerUserId = Guid.NewGuid(), ActivateImmediately = true }));
        ResultAssert.Ok(await _env.CreateTenantService().ArchiveAsync(
            dto.Id, new ChangeTenantStatusRequest { Reason = "closed" }));

        ResultAssert.Ok(await _env.CreateTenantService().DeleteAsync(dto.Id));

        Assert.Empty(_env.Read(db => db.Tenants.Where(t => t.Id == dto.Id).ToList()));

        var last = Outbox(dto.Id).Last();
        Assert.Equal(TenantOutboxEventTypes.TenantDeleted, last.EventType);

        var payload = JsonSerializer.Deserialize<TenantLifecycleEventPayload>(last.PayloadJson)!;
        Assert.Equal("Archived", payload.FromStatus);
        Assert.Equal("Deleted", payload.Status);
    }

    [Fact]
    public async Task ARefusedStatusChange_LeavesNoEvent()
    {
        var dto = ResultAssert.Ok(await _env.CreateTenantService().CreateAsync(
            new CreateTenantRequest { Name = "Acme Foods", OwnerUserId = Guid.NewGuid(), ActivateImmediately = true }));
        var before = Outbox(dto.Id).Count;

        ResultAssert.Fails(await _env.CreateTenantService().DeleteAsync(dto.Id), TenantErrorKind.Conflict);

        Assert.Equal(before, Outbox(dto.Id).Count);
    }
}