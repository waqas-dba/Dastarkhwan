using Microsoft.Extensions.DependencyInjection;

namespace CoreKit.Tenant.Tests;

public sealed class IntegrationEventTests
{
    private sealed class KernelRecorder<TEvent> : IEventHandler<TEvent> where TEvent : IIntegrationEvent
    {
        private readonly List<IIntegrationEvent> _seen;

        public KernelRecorder(List<IIntegrationEvent> seen) => _seen = seen;

        public Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default)
        {
            _seen.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class KernelThrower<TEvent> : IEventHandler<TEvent> where TEvent : IIntegrationEvent
    {
        public Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
    }

    private static readonly DateTime Moment = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<IIntegrationEvent> _kernelEvents = new();
    private readonly List<ITenantEvent> _tenantEvents = new();

    private TenantEventPublisher PublisherWith(Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IEventHandler<TenantCreated>>(new KernelRecorder<TenantCreated>(_kernelEvents));
        services.AddSingleton<IEventHandler<TenantDeleted>>(new KernelRecorder<TenantDeleted>(_kernelEvents));
        services.AddSingleton<IEventHandler<TenantMemberRemoved>>(new KernelRecorder<TenantMemberRemoved>(_kernelEvents));
        services.AddSingleton<ITenantEventHandler<TenantCreatedEvent>>(new RecordingHandler<TenantCreatedEvent>(_tenantEvents));
        extra?.Invoke(services);

        return new TenantEventPublisher(services.BuildServiceProvider(), NullLogger<TenantEventPublisher>.Instance);
    }

    [Fact]
    public async Task TenantCreated_IsAnnouncedWithOnlyTheId()
    {
        var tenantId = Guid.NewGuid();

        await PublisherWith().PublishAsync(new TenantCreatedEvent(tenantId, "acme", "Acme", TenantStatus.Active, Moment));

        Assert.Equal(new TenantCreated(tenantId, Moment), Assert.Single(_kernelEvents));
    }

    [Fact]
    public async Task TenantDeleted_IsAnnounced()
    {
        var tenantId = Guid.NewGuid();

        await PublisherWith().PublishAsync(new TenantDeletedEvent(tenantId, "acme", Moment));

        Assert.Equal(new TenantDeleted(tenantId, Moment), Assert.Single(_kernelEvents));
    }

    [Fact]
    public async Task MemberRemoved_IsAnnouncedWithTheUser()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await PublisherWith().PublishAsync(new TenantMemberRemovedEvent(tenantId, userId, Moment));

        Assert.Equal(new TenantMemberRemoved(tenantId, userId, Moment), Assert.Single(_kernelEvents));
    }

    [Fact]
    public async Task OtherTenantEvents_AreNotAnnouncedToOtherModules()
    {
        await PublisherWith().PublishAsync(new TenantUpdatedEvent(Guid.NewGuid(), "acme", "Acme", Moment));
        await PublisherWith().PublishAsync(
            new TenantStatusChangedEvent(Guid.NewGuid(), TenantStatus.Active, TenantStatus.Suspended, null, Moment));

        Assert.Empty(_kernelEvents);
    }

    [Fact]
    public async Task TenantsOwnHandlers_StillReceiveTheRichEvent()
    {
        await PublisherWith().PublishAsync(new TenantCreatedEvent(Guid.NewGuid(), "acme", "Acme", TenantStatus.Active, Moment));

        Assert.Single(_tenantEvents);
        Assert.Single(_kernelEvents);
    }

    [Fact]
    public async Task AFailingHandlerInAnotherModule_DoesNotStopOthersOrTheCaller()
    {
        var publisher = PublisherWith(s =>
            s.AddSingleton<IEventHandler<TenantDeleted>>(new KernelThrower<TenantDeleted>()));

        await publisher.PublishAsync(new TenantDeletedEvent(Guid.NewGuid(), "acme", Moment));

        Assert.Single(_kernelEvents);   // the healthy handler still ran
    }
}

public sealed class TenantMembershipReaderTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private TenantMembershipReader Reader => new(_env.Members);

    [Fact]
    public async Task Find_ReturnsOwnerFlag()
    {
        var tenant = _env.AddTenant();
        var owner = Guid.NewGuid();
        var staff = Guid.NewGuid();
        _env.AddMember(tenant.Id, owner, isOwner: true);
        _env.AddMember(tenant.Id, staff);

        Assert.Equal(new TenantMemberInfo(true), await Reader.FindAsync(tenant.Id, owner));
        Assert.Equal(new TenantMemberInfo(false), await Reader.FindAsync(tenant.Id, staff));
    }

    [Fact]
    public async Task Find_ReturnsNullForNonMembers_AndForEmptyIds()
    {
        var tenant = _env.AddTenant();
        _env.AddMember(tenant.Id, Guid.NewGuid());

        Assert.Null(await Reader.FindAsync(tenant.Id, Guid.NewGuid()));
        Assert.Null(await Reader.FindAsync(tenant.Id, Guid.Empty));
        Assert.Null(await Reader.FindAsync(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public async Task Find_IsAServerSideRead_ItIgnoresTheCallersTenantBoundary()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        var user = Guid.NewGuid();
        _env.AddMember(tenant.Id, user);
        _env.Actor.IsPlatformAdmin = false;
        using var _ = _env.CurrentTenant.Change(Guid.NewGuid());   // bound to another tenant

        Assert.NotNull(await Reader.FindAsync(tenant.Id, user));
    }

    [Fact]
    public async Task ServiceFlow_RemovingAMember_AnnouncesItToOtherModules()
    {
        var tenant = _env.AddTenant();
        var owner = Guid.NewGuid();
        var staff = Guid.NewGuid();
        _env.AddMember(tenant.Id, owner, isOwner: true);
        _env.AddMember(tenant.Id, staff);

        var result = await _env.CreateMembershipService().RemoveMemberAsync(tenant.Id, staff);

        Assert.True(result.IsSuccess);
        Assert.Single(_env.EventsOf<TenantMemberRemovedEvent>());
    }
}