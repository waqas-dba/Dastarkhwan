using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CoreKit.Tenant.Tests.Support;

/// <summary>
/// One real object graph on an in-memory SQLite database, built like a single web request:
/// the services share one DbContext, and the tests read results back through a separate one.
/// By default the actor is a platform administrator and no tenant is resolved.
/// </summary>
internal sealed class TestEnv : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TenantDbContext> _dbOptions;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ServiceProvider _handlerServices;

    public TestEnv(TenantOptions? config = null)
    {
        Config = config ?? new TenantOptions();

        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<TenantDbContext>().UseSqlite(_connection).Options;

        using (var setup = NewContext())
            setup.Database.EnsureCreated();

        Db = NewContext();
        Time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));
        Actor = new FakeTenantActor { IsPlatformAdmin = true };
        CurrentTenant = new CurrentTenant();
        PlatformAccess = new TenantPlatformAccess(Actor);
        Guard = new TenantAccessGuard(CurrentTenant, PlatformAccess, Actor);

        Tenants = new TenantRepository(Db);
        Settings = new TenantSettingRepository(Db);
        Members = new TenantMemberRepository(Db);
        AuditEntries = new TenantAuditRepository(Db);
        UnitOfWork = new TenantUnitOfWork(Db, NullLogger<TenantUnitOfWork>.Instance);
        Audit = new TenantAuditRecorder(AuditEntries, Actor, Time);
        InfoProvider = new TenantInfoProvider(Tenants, _cache, Options.Create(Config));

        var handlers = new ServiceCollection();
        Record<TenantCreatedEvent>(handlers);
        Record<TenantUpdatedEvent>(handlers);
        Record<TenantStatusChangedEvent>(handlers);
        Record<TenantDeletedEvent>(handlers);
        Record<TenantMemberAddedEvent>(handlers);
        Record<TenantMemberRemovedEvent>(handlers);
        Record<TenantSettingChangedEvent>(handlers);
        _handlerServices = handlers.BuildServiceProvider();

        Publisher = new TenantEventPublisher(_handlerServices, NullLogger<TenantEventPublisher>.Instance);
    }

    public TenantOptions Config { get; }

    public TenantDbContext Db { get; }

    public FixedTimeProvider Time { get; }

    public FakeTenantActor Actor { get; }

    public CurrentTenant CurrentTenant { get; }

    public TenantPlatformAccess PlatformAccess { get; }

    public TenantAccessGuard Guard { get; }

    public TenantRepository Tenants { get; }

    public TenantSettingRepository Settings { get; }

    public TenantMemberRepository Members { get; }

    public TenantAuditRepository AuditEntries { get; }

    public TenantUnitOfWork UnitOfWork { get; }

    public TenantAuditRecorder Audit { get; }

    public TenantInfoProvider InfoProvider { get; }

    public TenantEventPublisher Publisher { get; }

    public List<ITenantEvent> Published { get; } = new();

    public DateTime UtcNow => Time.GetUtcNow().UtcDateTime;

    public TenantDbContext NewContext() => new(_dbOptions);

    public List<T> EventsOf<T>() where T : ITenantEvent => Published.OfType<T>().ToList();

    public TenantService CreateTenantService() => new(
        Tenants, Members, Audit, UnitOfWork, Publisher, InfoProvider, Guard, Options.Create(Config), Time);

    public TenantSettingsService CreateSettingsService() => new(
        Tenants, Settings, Audit, UnitOfWork, Publisher, Guard, Options.Create(Config), Time);

    public TenantMembershipService CreateMembershipService() => new(
        Tenants, Members, Audit, UnitOfWork, Publisher, Guard, Time);

    public TenantAuditLog CreateAuditLog() => new(AuditEntries, Guard);

    // Direct database access: seed with one context, read back with another, so nothing is served from a tracker.

    public void Seed(Action<TenantDbContext> action)
    {
        using var db = NewContext();
        action(db);
        db.SaveChanges();
    }

    public T Read<T>(Func<TenantDbContext, T> query)
    {
        using var db = NewContext();
        return query(db);
    }

    public TenantEntity AddTenant(
        string name = "Acme Foods", string? slug = null, TenantStatus status = TenantStatus.Active, string? reason = null)
    {
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug ?? TenantSlug.FromName(name),
            Status = status,
            StatusReason = reason,
            CreatedAt = UtcNow,
            StatusChangedAt = UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        Seed(db => db.Tenants.Add(tenant));
        return tenant;
    }

    public TenantMember AddMember(
        Guid tenantId, Guid userId, bool isOwner = false, bool isDefault = false, DateTime? joinedAt = null)
    {
        var member = new TenantMember
        {
            TenantId = tenantId,
            UserId = userId,
            IsOwner = isOwner,
            IsDefault = isDefault,
            JoinedAt = joinedAt ?? UtcNow
        };

        Seed(db => db.Members.Add(member));
        return member;
    }

    public TenantSetting AddSetting(Guid tenantId, string key, string value)
    {
        var setting = new TenantSetting { TenantId = tenantId, Key = key, Value = value, UpdatedAt = UtcNow };
        Seed(db => db.Settings.Add(setting));
        return setting;
    }

    public void Dispose()
    {
        Db.Dispose();
        _handlerServices.Dispose();
        _cache.Dispose();
        _connection.Dispose();
    }

    private void Record<TEvent>(IServiceCollection services) where TEvent : ITenantEvent
        => services.AddSingleton<ITenantEventHandler<TEvent>>(new RecordingHandler<TEvent>(Published));
}