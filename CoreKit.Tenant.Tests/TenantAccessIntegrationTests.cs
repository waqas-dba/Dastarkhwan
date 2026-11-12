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
        current.Set(Guid.Empty);

        var context = new TenantAccessScopeContext(current);

        Assert.Null(context.Current);
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
        _reader.Members[(scope.Id, userId)] = true;

        var result = await _membership.FindAsync(scope, userId);

        Assert.NotNull(result);
        Assert.False(result!.IsOwner); // Tenant membership is NOT Access owner
    }

    [Fact]
    public async Task FindAsync_ReturnsMemberFalse_WhenUserIsATenantOwner()
    {
        var scope = AccessScope.ForTenant(Guid.NewGuid());
        var userId = Guid.NewGuid();
        _reader.Members[(scope.Id, userId)] = true; // tenant owner

        var result = await _membership.FindAsync(scope, userId);

        Assert.NotNull(result);
        Assert.False(result!.IsOwner); // Tenant owner is NOT Access owner — no super permissions
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
        _reader.Members[(Guid.NewGuid(), userId)] = true;

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
        _reader.Members[(tenantId, userId)] = true;

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

public sealed class AccessIntegrationDIRegistrationTests
{
    [Fact]
    public void AddTenant_RegistersTenantAccessScopeContext_AndTenantAccessMembership()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:MaxSettingsPerTenant"] = "1",
                ["Tenant:InfoCacheSeconds"] = "0",
                ["Tenant:Resolution:HeaderName"] = "X-Tenant-Id",
                ["Tenant:Resolution:ClaimType"] = "tenant_id",
            })
            .Build();

        services.AddTenant(config, options => options.UseSqlite("DataSource=:memory:"));

        var provider = services.BuildServiceProvider();

        var scopeContext = provider.GetRequiredService<IAccessScopeContext>();
        var membership = provider.GetRequiredService<IAccessMembership>();

        Assert.IsType<TenantAccessScopeContext>(scopeContext);
        Assert.IsType<TenantAccessMembership>(membership);
    }

    [Fact]
    public void AddTenant_ThenAddAccess_KeepsTenantAdapters()
    {
        var services = new ServiceCollection();

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

        services.AddTenant(config, options => options.UseSqlite("DataSource=:memory:"));
        services.AddAccess(config, options => options.UseSqlite("DataSource=:memory:"));

        var provider = services.BuildServiceProvider();

        var scopeContext = provider.GetRequiredService<IAccessScopeContext>();
        var membership = provider.GetRequiredService<IAccessMembership>();

        Assert.IsType<TenantAccessScopeContext>(scopeContext);
        Assert.IsType<TenantAccessMembership>(membership);
    }
}
