using CoreKit.IAM.Entities;
using CoreKit.Tenant.Extensions;
using CoreKit.Tenant.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreKit.Tenant.Tests;

public sealed class TenantClaimsContributorTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private async Task<string?> ClaimFor(Guid userId)
    {
        var claims = await new TenantClaimsContributor(_env.Members).GetClaimsAsync(new User { Id = userId });

        return claims.SingleOrDefault(c => c.Type == TenantClaimTypes.TenantId)?.Value;
    }

    [Fact]
    public async Task UserWithoutTenants_GetsNoClaim()
        => Assert.Null(await ClaimFor(Guid.NewGuid()));

    [Fact]
    public async Task UserWithOneTenant_GetsIt()
    {
        var user = Guid.NewGuid();
        var tenant = _env.AddTenant();
        _env.AddMember(tenant.Id, user);

        Assert.Equal(tenant.Id.ToString(), await ClaimFor(user));
    }

    [Fact]
    public async Task UserWithSeveralTenants_GetsTheDefaultOne()
    {
        var user = Guid.NewGuid();
        var first = _env.AddTenant("First", "first");
        var second = _env.AddTenant("Second", "second");
        _env.AddMember(first.Id, user, joinedAt: _env.UtcNow);
        _env.AddMember(second.Id, user, isDefault: true, joinedAt: _env.UtcNow.AddDays(1));

        Assert.Equal(second.Id.ToString(), await ClaimFor(user));
    }

    [Fact]
    public async Task WithoutADefault_TheOldestMembershipWins()
    {
        var user = Guid.NewGuid();
        var first = _env.AddTenant("First", "first");
        var second = _env.AddTenant("Second", "second");
        _env.AddMember(second.Id, user, joinedAt: _env.UtcNow.AddDays(1));
        _env.AddMember(first.Id, user, joinedAt: _env.UtcNow);

        Assert.Equal(first.Id.ToString(), await ClaimFor(user));
    }

    [Fact]
    public async Task ArchivedTenantsAreSkipped_EvenAsDefault()
    {
        var user = Guid.NewGuid();
        var archived = _env.AddTenant("Old", "old", TenantStatus.Archived);
        var live = _env.AddTenant("Live", "live");
        _env.AddMember(archived.Id, user, isDefault: true);
        _env.AddMember(live.Id, user);

        Assert.Equal(live.Id.ToString(), await ClaimFor(user));
    }

    [Fact]
    public async Task UserWithOnlyArchivedTenants_GetsNoClaim()
    {
        var user = Guid.NewGuid();
        _env.AddMember(_env.AddTenant("Old", "old", TenantStatus.Archived).Id, user, isDefault: true);

        Assert.Null(await ClaimFor(user));
    }

    [Theory]
    [InlineData(TenantStatus.Pending)]
    [InlineData(TenantStatus.Suspended)]
    public async Task PendingAndSuspendedTenantsStillGetAClaim_TheMiddlewareEnforcesStatus(TenantStatus status)
    {
        var user = Guid.NewGuid();
        var tenant = _env.AddTenant(status: status);
        _env.AddMember(tenant.Id, user);

        Assert.Equal(tenant.Id.ToString(), await ClaimFor(user));
    }

    [Fact]
    public async Task OtherUsersMembershipsAreIgnored()
    {
        var tenant = _env.AddTenant();
        _env.AddMember(tenant.Id, Guid.NewGuid());

        Assert.Null(await ClaimFor(Guid.NewGuid()));
    }
}

public sealed class IamTenantActorTests
{
    [Fact]
    public void WithoutIam_ThereIsNoUserAndNoPlatformAccess()
    {
        var actor = new IamTenantActor();

        Assert.Null(actor.UserId);
        Assert.False(actor.IsPlatformAdmin);
    }

    [Fact]
    public void TakesTheUserFromIam()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, new IamTenantActor(new FakeCurrentUserService { UserId = id }).UserId);
    }

    [Fact]
    public void OnlyThePlatformPermissionMakesAPlatformAdmin()
    {
        var withPermission = new FakeCurrentUserService { Permissions = new[] { TenantPermissionNames.Platform } };
        var withOthers = new FakeCurrentUserService
        {
            Permissions = new[] { TenantPermissionNames.Update, TenantPermissionNames.Read }
        };

        Assert.True(new IamTenantActor(withPermission).IsPlatformAdmin);
        Assert.False(new IamTenantActor(withOthers).IsPlatformAdmin);
    }
}

public sealed class TenantUnitOfWorkTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private TenantUnitOfWork UnitOfWorkFor(TenantDbContext db)
        => new(db, NullLogger<TenantUnitOfWork>.Instance);

    private TenantEntity NewTenant(string slug) => new()
    {
        Id = Guid.NewGuid(),
        Name = slug,
        Slug = slug,
        Status = TenantStatus.Active,
        CreatedAt = _env.UtcNow,
        StatusChangedAt = _env.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    [Fact]
    public async Task Save_StoresEverythingStaged()
    {
        using var db = _env.NewContext();
        db.Tenants.Add(NewTenant("acme"));

        Assert.Equal(SaveOutcome.Saved, await UnitOfWorkFor(db).SaveAsync());
        Assert.Single(_env.Read(c => c.Tenants.ToList()));
    }

    [Fact]
    public async Task Save_ReportsADuplicateSlugRaceAsAConstraintViolation_AndResetsTheContext()
    {
        _env.AddTenant("Acme", "acme");
        using var db = _env.NewContext();
        db.Tenants.Add(NewTenant("acme"));

        Assert.Equal(SaveOutcome.ConstraintViolation, await UnitOfWorkFor(db).SaveAsync());
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Save_ReportsAConcurrentEditAsAConflict()
    {
        var tenant = _env.AddTenant("Acme", "acme");

        using var first = _env.NewContext();
        using var second = _env.NewContext();
        var a = first.Tenants.Single(t => t.Id == tenant.Id);
        var b = second.Tenants.Single(t => t.Id == tenant.Id);

        b.Name = "Second wins";
        b.ConcurrencyStamp = Guid.NewGuid();
        Assert.Equal(SaveOutcome.Saved, await UnitOfWorkFor(second).SaveAsync());

        a.Name = "First loses";
        a.ConcurrencyStamp = Guid.NewGuid();

        Assert.Equal(SaveOutcome.ConcurrencyConflict, await UnitOfWorkFor(first).SaveAsync());
        Assert.Equal("Second wins", _env.Read(c => c.Tenants.Single(t => t.Id == tenant.Id)).Name);
    }

    [Fact]
    public void ToError_MapsEveryOutcome()
    {
        Assert.Null(SaveOutcome.Saved.ToError());
        Assert.Equal(TenantErrors.SaveConflict, SaveOutcome.ConstraintViolation.ToError());
        Assert.Equal(TenantErrors.SaveConflict, SaveOutcome.ConcurrencyConflict.ToError());
    }
}

public sealed class TenantDependencyInjectionTests
{
    private static ServiceProvider BuildProvider(SqliteConnection connection, Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTenant(configuration, db => db.UseSqlite(connection));

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task EverythingResolves_AndWorksEndToEnd()
    {
        using var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        connection.Open();
        using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<TenantDbContext>().Database.EnsureCreated();

        Assert.NotNull(sp.GetRequiredService<ICurrentTenant>());
        Assert.NotNull(sp.GetRequiredService<ITenantSettingsService>());
        Assert.NotNull(sp.GetRequiredService<ITenantMembershipService>());
        Assert.NotNull(sp.GetRequiredService<ITenantAuditLog>());
        Assert.NotNull(sp.GetRequiredService<ITenantInfoProvider>());
        Assert.NotNull(sp.GetRequiredService<ITenantPlatformAccess>());

        // Without IAM there is no actor, so nobody has platform access until trusted code grants it.
        var service = sp.GetRequiredService<ITenantService>();
        ResultAssert.Fails(
            await service.CreateAsync(new CreateTenantRequest { Name = "Acme" }), TenantErrorKind.Forbidden);

        using (sp.GetRequiredService<ITenantPlatformAccess>().Grant())
        {
            var created = ResultAssert.Ok(await service.CreateAsync(new CreateTenantRequest { Name = "Acme" }));
            Assert.Equal("acme", created.Slug);
        }
    }

    [Fact]
    public void CurrentTenant_IsOneObjectPerScope_AndNotSharedBetweenScopes()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var provider = BuildProvider(connection);

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<ICurrentTenant>(),
            first.ServiceProvider.GetRequiredService<ICurrentTenant>());

        first.ServiceProvider.GetRequiredService<ITenantContextSetter>().Set(Guid.NewGuid());

        Assert.True(first.ServiceProvider.GetRequiredService<ICurrentTenant>().IsResolved);
        Assert.False(second.ServiceProvider.GetRequiredService<ICurrentTenant>().IsResolved);
    }

    [Fact]
    public void RegistersBothResolutionStrategies_AndTheIamClaimsContributor()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();

        var strategies = scope.ServiceProvider.GetServices<ITenantResolutionStrategy>().ToList();
        var contributors = scope.ServiceProvider.GetServices<CoreKit.IAM.Hooks.IAccessTokenClaimsContributor>().ToList();

        Assert.Equal(2, strategies.Count);
        Assert.Contains(contributors, c => c is TenantClaimsContributor);
    }

    [Fact]
    public void BindsOptionsFromConfiguration()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var provider = BuildProvider(connection, new Dictionary<string, string?>
        {
            ["Tenant:Resolution:TrustHeader"] = "true",
            ["Tenant:Resolution:HeaderName"] = "X-Org",
            ["Tenant:MaxSettingsPerTenant"] = "5",
            ["Tenant:AdditionalReservedSlugs:0"] = "shop"
        });

        var options = provider.GetRequiredService<IOptions<TenantOptions>>().Value;

        Assert.True(options.Resolution.TrustHeader);
        Assert.Equal("X-Org", options.Resolution.HeaderName);
        Assert.Equal(5, options.MaxSettingsPerTenant);
        Assert.Contains("shop", options.GetReservedSlugs());
    }

    [Theory]
    [InlineData("Tenant:MaxSettingsPerTenant", "0")]
    [InlineData("Tenant:InfoCacheSeconds", "-1")]
    [InlineData("Tenant:Resolution:HeaderName", "")]
    public void InvalidOptionsAreRejected(string key, string value)
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var provider = BuildProvider(connection, new Dictionary<string, string?> { [key] = value });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TenantOptions>>().Value);
    }

    [Fact]
    public void MissingConnectionStringFailsWithAClearMessage()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTenant(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<TenantDbContext>());

        Assert.Contains("ConnectionStrings:Tenant", error.Message);
    }
}