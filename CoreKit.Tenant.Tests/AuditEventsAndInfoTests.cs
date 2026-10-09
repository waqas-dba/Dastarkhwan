using Microsoft.Extensions.DependencyInjection;

namespace CoreKit.Tenant.Tests;

public sealed class TenantAuditLogTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task List_ShowsNewestFirst_AndPages()
    {
        var tenants = _env.CreateTenantService();
        var created = ResultAssert.Ok(await tenants.CreateAsync(new CreateTenantRequest { Name = "Acme" }));
        _env.Time.Advance(TimeSpan.FromMinutes(1));
        await tenants.UpdateAsync(created.Id, new UpdateTenantRequest { Name = "Acme Ltd" });
        _env.Time.Advance(TimeSpan.FromMinutes(1));
        await tenants.SuspendAsync(created.Id, new ChangeTenantStatusRequest());

        var log = _env.CreateAuditLog();
        var all = ResultAssert.Ok(await log.ListAsync(created.Id, new TenantAuditQuery()));
        var secondPage = ResultAssert.Ok(await log.ListAsync(created.Id, new TenantAuditQuery { Page = 2, PageSize = 2 }));

        Assert.Equal(
            new[] { TenantAuditActions.Suspended, TenantAuditActions.Updated, TenantAuditActions.Created },
            all.Items.Select(e => e.Action));
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(new[] { TenantAuditActions.Created }, secondPage.Items.Select(e => e.Action));
    }

    [Fact]
    public async Task List_ClampsThePageSize()
    {
        var result = ResultAssert.Ok(await _env.CreateAuditLog().ListAsync(
            Guid.NewGuid(), new TenantAuditQuery { Page = 0, PageSize = 100000 }));

        Assert.Equal(1, result.Page);
        Assert.Equal(200, result.PageSize);
    }

    [Fact]
    public async Task List_OnlyShowsTheRequestedTenantsHistory()
    {
        var a = _env.AddTenant("A One", "a-one");
        var b = _env.AddTenant("B One", "b-one");
        _env.Seed(db =>
        {
            db.AuditEntries.Add(new TenantAuditEntry { Id = Guid.NewGuid(), TenantId = a.Id, Action = "a", OccurredAt = _env.UtcNow });
            db.AuditEntries.Add(new TenantAuditEntry { Id = Guid.NewGuid(), TenantId = b.Id, Action = "b", OccurredAt = _env.UtcNow });
        });

        var result = ResultAssert.Ok(await _env.CreateAuditLog().ListAsync(a.Id, new TenantAuditQuery()));

        Assert.Equal(new[] { "a" }, result.Items.Select(e => e.Action));
    }

    [Fact]
    public async Task List_BoundCallersSeeOnlyTheirOwnHistory()
    {
        var own = _env.AddTenant("Own", "own");
        var other = _env.AddTenant("Other", "other");
        _env.Actor.IsPlatformAdmin = false;
        using var _ = _env.CurrentTenant.Change(own.Id);

        ResultAssert.Ok(await _env.CreateAuditLog().ListAsync(own.Id, new TenantAuditQuery()));
        ResultAssert.Fails(
            await _env.CreateAuditLog().ListAsync(other.Id, new TenantAuditQuery()), TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task Recorder_TruncatesVeryLongDetails()
    {
        var tenant = _env.AddTenant();

        _env.Audit.Record(tenant.Id, "custom", new string('x', 5000));
        await _env.UnitOfWork.SaveAsync();

        var entry = _env.Read(db => db.AuditEntries.Single());
        Assert.Equal(2000, entry.Details!.Length);
    }

    [Fact]
    public async Task Recorder_WorksWithoutAKnownActor()
    {
        var tenant = _env.AddTenant();

        _env.Audit.Record(tenant.Id, "custom");
        await _env.UnitOfWork.SaveAsync();

        Assert.Null(_env.Read(db => db.AuditEntries.Single()).ActorUserId);
    }

    [Fact]
    public async Task Audit_IsSavedAtomicallyWithTheChange()
    {
        _env.AddTenant("Acme", "acme");

        // The second create fails on the unique slug, so its audit entry must not exist either.
        await _env.NewScopeService().CreateAsync(new CreateTenantRequest { Name = "Dup", Slug = "acme" });

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }
}

public static class TestEnvExtensions
{
    public static TenantService NewScopeService(this TestEnv env) => env.CreateTenantService();
}

public sealed class TenantEventPublisherTests
{
    private static TenantCreatedEvent Event() => new(Guid.NewGuid(), "acme", "Acme", TenantStatus.Active, DateTime.UtcNow);

    private static TenantEventPublisher PublisherWith(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        return new TenantEventPublisher(services.BuildServiceProvider(), NullLogger<TenantEventPublisher>.Instance);
    }

    [Fact]
    public async Task Publish_WithNoHandlersDoesNothing()
        => await PublisherWith(_ => { }).PublishAsync(Event());

    [Fact]
    public async Task Publish_ReachesEveryHandler()
    {
        var seen = new List<ITenantEvent>();
        var publisher = PublisherWith(s =>
        {
            s.AddSingleton<ITenantEventHandler<TenantCreatedEvent>>(new RecordingHandler<TenantCreatedEvent>(seen));
            s.AddSingleton<ITenantEventHandler<TenantCreatedEvent>>(new RecordingHandler<TenantCreatedEvent>(seen));
        });

        await publisher.PublishAsync(Event());

        Assert.Equal(2, seen.Count);
    }

    [Fact]
    public async Task Publish_AFailingHandlerDoesNotStopTheOthersOrTheCaller()
    {
        var seen = new List<ITenantEvent>();
        var publisher = PublisherWith(s =>
        {
            s.AddSingleton<ITenantEventHandler<TenantCreatedEvent>>(
                new ThrowingHandler<TenantCreatedEvent>(new InvalidOperationException("boom")));
            s.AddSingleton<ITenantEventHandler<TenantCreatedEvent>>(new RecordingHandler<TenantCreatedEvent>(seen));
        });

        await publisher.PublishAsync(Event());

        Assert.Single(seen);
    }

    [Fact]
    public async Task Publish_LetsCancellationThrough()
    {
        var publisher = PublisherWith(s => s.AddSingleton<ITenantEventHandler<TenantCreatedEvent>>(
            new ThrowingHandler<TenantCreatedEvent>(new OperationCanceledException())));

        await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(Event()));
    }

    [Fact]
    public async Task Publish_OnlyReachesHandlersOfThatEventType()
    {
        var seen = new List<ITenantEvent>();
        var publisher = PublisherWith(s =>
            s.AddSingleton<ITenantEventHandler<TenantDeletedEvent>>(new RecordingHandler<TenantDeletedEvent>(seen)));

        await publisher.PublishAsync(Event());

        Assert.Empty(seen);
    }
}

public sealed class TenantInfoProviderTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private void ChangeNameBehindTheCache(Guid id, string name)
        => _env.Seed(db => db.Tenants.Single(t => t.Id == id).Name = name);

    [Fact]
    public async Task FindsByIdAndBySlug_NormalizingTheSlug()
    {
        var tenant = _env.AddTenant("Acme", "acme", TenantStatus.Suspended);

        var byId = await _env.InfoProvider.GetByIdAsync(tenant.Id);
        var bySlug = await _env.InfoProvider.GetBySlugAsync("  ACME ");

        Assert.Equal(new TenantInfo(tenant.Id, "acme", "Acme", TenantStatus.Suspended), byId);
        Assert.Equal(byId, bySlug);
    }

    [Fact]
    public async Task UnknownTenantsAreNull()
    {
        Assert.Null(await _env.InfoProvider.GetByIdAsync(Guid.NewGuid()));
        Assert.Null(await _env.InfoProvider.GetBySlugAsync("nobody"));
        Assert.Null(await _env.InfoProvider.GetBySlugAsync("   "));
    }

    [Fact]
    public async Task AnswersAreRememberedUntilInvalidated()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        await _env.InfoProvider.GetByIdAsync(tenant.Id);

        ChangeNameBehindTheCache(tenant.Id, "Changed");

        Assert.Equal("Acme", (await _env.InfoProvider.GetByIdAsync(tenant.Id))!.Name);

        _env.InfoProvider.Invalidate(tenant.Id, "acme");

        Assert.Equal("Changed", (await _env.InfoProvider.GetByIdAsync(tenant.Id))!.Name);
    }

    [Fact]
    public async Task OneLookupFillsBothKeys()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        await _env.InfoProvider.GetByIdAsync(tenant.Id);

        ChangeNameBehindTheCache(tenant.Id, "Changed");

        Assert.Equal("Acme", (await _env.InfoProvider.GetBySlugAsync("acme"))!.Name);
    }

    [Fact]
    public async Task InvalidateByIdAndSlugForgetsBoth()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        await _env.InfoProvider.GetByIdAsync(tenant.Id);
        ChangeNameBehindTheCache(tenant.Id, "Changed");

        _env.InfoProvider.Invalidate(tenant.Id, "ACME");

        Assert.Equal("Changed", (await _env.InfoProvider.GetBySlugAsync("acme"))!.Name);
    }

    [Fact]
    public async Task ZeroSecondsTurnsTheCacheOff()
    {
        using var env = new TestEnv(new TenantOptions { InfoCacheSeconds = 0 });
        var tenant = env.AddTenant("Acme", "acme");
        await env.InfoProvider.GetByIdAsync(tenant.Id);

        env.Seed(db => db.Tenants.Single(t => t.Id == tenant.Id).Name = "Changed");

        Assert.Equal("Changed", (await env.InfoProvider.GetByIdAsync(tenant.Id))!.Name);
    }

    [Fact]
    public async Task MissesAreNotRemembered()
    {
        Assert.Null(await _env.InfoProvider.GetBySlugAsync("acme"));

        _env.AddTenant("Acme", "acme");

        Assert.NotNull(await _env.InfoProvider.GetBySlugAsync("acme"));
    }
}