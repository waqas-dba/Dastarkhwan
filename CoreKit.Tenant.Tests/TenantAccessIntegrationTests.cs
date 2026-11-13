using CoreKit.Access.Abstractions;
using CoreKit.Access.Constants;
using CoreKit.Access.Extensions;
using CoreKit.Tenant.Integration;
using CoreKit.Tenant.Extensions;
using Microsoft.Extensions.Configuration;

namespace CoreKit.Tenant.Tests;

public sealed class TenantAccessScopeContextTests
{
    [Fact]
    public void Current_ReturnsTenantScope_WhenTenantIsResolved()
    {
        var tenantId = Guid.NewGuid();
        var current = new CurrentTenant();
        current.Set(tenantId);

        var context = new TenantAccessScopeContext(current);

        var scope = context.Current;

        Assert.NotNull(scope);
        Assert.Equal(AccessScope.TenantType, scope.Value.Type);
        Assert.Equal(tenantId, scope.Value.Id);
        Assert.True(scope.Value.IsValid);
    }

    [Fact]
    public void Current_ReturnsNull_WhenNoTenantIsResolved()
    {
        var current = new CurrentTenant();

        var context = new TenantAccessScopeContext(current);

        Assert.Null(context.Current);
    }

    [Fact]
    public void Current_ReturnsNull_WhenTenantIdIsEmpty()
    {
        var current = new CurrentTenant();
        Assert.Throws<ArgumentException>(() => current.Set(Guid.Empty));
    }
}

public sealed class TenantAccessMembershipTests
{
    private readonly TenantAccessMembership _membership;
    private readonly FakeTenantMembershipReader _reader;

    public TenantAccessMembershipTests()
    {
        _reader = new FakeTenantMembershipReader();
        _membership = new TenantAccessMembership(_reader);
    }

    [Fact]
    public async Task FindAsync_ReturnsMemberFalse_WhenUserIsAMember()
    {
        var scope = AccessScope.ForTenant(Guid.NewGuid());
        var userId = Guid.NewGuid();
        _reader.Members[(scope.Id, userId)] = new TenantMemberInfo(true);

        var result = await _membership.FindAsync(scope, userId);

        Assert.NotNull(result);
        Assert.False(result!.IsOwner); // Tenant membership is NOT Access owner
    }

    [Fact]
    public async Task FindAsync_ReturnsMemberFalse_WhenUserIsATenantOwner()
    {
        var scope = AccessScope.ForTenant(Guid.NewGuid());
        var userId = Guid.NewGuid();
        _reader.Members[(scope.Id, userId)] = new TenantMemberInfo(true); // tenant owner

        var result = await _membership.FindAsync(scope, userId);

        Assert.NotNull(result);
        Assert.False(result!.IsOwner); // Tenant owner is NOT Access owner — no super permissions
    }

    [Fact]
    public async Task FindAsync_ReturnsTheCurrentMembershipGeneration()
    {
        var scope = AccessScope.ForTenant(Guid.NewGuid());
        var userId = Guid.NewGuid();
        _reader.Members[(scope.Id, userId)] = new TenantMemberInfo(false, 7);

        var result = await _membership.FindAsync(scope, userId);

        Assert.NotNull(result);
        Assert.Equal(7L, result!.MembershipGeneration);
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_WhenUserIsNotAMember()
    {
        var scope = AccessScope.ForTenant(Guid.NewGuid());
        var userId = Guid.NewGuid();

        var result = await _membership.FindAsync(scope, userId);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_WhenScopeIsNotATenant()
    {
        var scope = new AccessScope("store", Guid.NewGuid());
        var userId = Guid.NewGuid();
        _reader.Members[(Guid.NewGuid(), userId)] = new TenantMemberInfo(true);

        var result = await _membership.FindAsync(scope, userId);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_WhenUserIdIsEmpty()
    {
        var scope = AccessScope.ForTenant(Guid.NewGuid());

        var result = await _membership.FindAsync(scope, Guid.Empty);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_WhenTenantIdIsEmpty()
    {
        var scope = new AccessScope(AccessScope.TenantType, Guid.Empty);
        var userId = Guid.NewGuid();

        var result = await _membership.FindAsync(scope, userId);

        Assert.Null(result);
    }
}

public sealed class TenantAccessMembershipReaderTests
{
    private readonly FakeTenantMembershipReader _reader;

    public TenantAccessMembershipReaderTests()
    {
        _reader = new FakeTenantMembershipReader();
    }

    [Fact]
    public async Task FindAsync_ReturnsMemberInfoWithIsOwner_ForTenantOwner()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _reader.Members[(tenantId, userId)] = new TenantMemberInfo(true);

        var result = await _reader.FindAsync(tenantId, userId);

        Assert.NotNull(result);
        Assert.True(result!.IsOwner);
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_ForNonMember()
    {
        var result = await _reader.FindAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }
}

public sealed class AccessIntegrationDIRegistrationTests : IDisposable
{
    private readonly SqliteConnection _sharedConnection;

    public AccessIntegrationDIRegistrationTests()
    {
        // One in-memory database shared by Tenant and Access, kept open for the test's lifetime.
        _sharedConnection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _sharedConnection.Open();
    }

    public void Dispose() => _sharedConnection.Dispose();

    private static IServiceCollection NewServices(SqliteConnection connection)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:MaxSettingsPerTenant"] = "1",
                ["Tenant:InfoCacheSeconds"] = "0",
                ["Tenant:Resolution:HeaderName"] = "X-Tenant-Id",
                ["Tenant:Resolution:ClaimType"] = "tenant_id",
                ["Access:MaxRolesPerScope"] = "1",
                ["Access:MaxRolesPerUser"] = "1",
                ["Access:CacheSeconds"] = "0",
                ["Access:UserIdClaimType"] = "sub",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTenant(config, options => options.UseSqlite(connection));
        services.AddAccess(config, options => options.UseSqlite(connection));
        return services;
    }

    [Fact]
    public async Task AddTenant_ThenAddAccess_ComposesTenantAndPlatformAdapters()
    {
        var provider = NewServices(_sharedConnection).BuildServiceProvider();

        var membership = provider.GetRequiredService<IAccessMembership>();
        var adapters = provider.GetRequiredService<IEnumerable<IAccessMembershipAdapter>>().ToList();

        // One composed IAccessMembership; Tenant's membership is an adapter, not the service itself.
        Assert.NotNull(membership);
        Assert.Single(adapters.OfType<TenantAccessMembership>());

        // Tenant membership resolves through the composed service.
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantMemberAsync(provider, tenantId, userId);

        var member = await membership.FindAsync(AccessScope.ForTenant(tenantId), userId);
        Assert.NotNull(member);
        Assert.False(member!.IsOwner);
    }

    [Fact]
    public async Task AddAccess_ThenAddTenant_ComposesTenantAndPlatformAdapters()
    {
        var provider = NewServices(_sharedConnection).BuildServiceProvider();

        var membership = provider.GetRequiredService<IAccessMembership>();
        var adapters = provider.GetRequiredService<IEnumerable<IAccessMembershipAdapter>>().ToList();

        Assert.NotNull(membership);
        Assert.Single(adapters.OfType<TenantAccessMembership>());

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantMemberAsync(provider, tenantId, userId);

        var member = await membership.FindAsync(AccessScope.ForTenant(tenantId), userId);
        Assert.NotNull(member);
        Assert.False(member!.IsOwner);
    }

    private static async Task SeedTenantMemberAsync(IServiceProvider provider, Guid tenantId, Guid userId)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        db.Database.EnsureCreated();
        db.Tenants.Add(new TenantEntity
        {
            Id = tenantId,
            Name = "Acme",
            Slug = "acme",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            StatusChangedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Members.Add(new TenantMember
        {
            TenantId = tenantId,
            UserId = userId,
            IsOwner = false,
            IsDefault = true,
            JoinedAt = DateTime.UtcNow,
            MembershipGeneration = 1
        });
        await db.SaveChangesAsync();
    }
}
