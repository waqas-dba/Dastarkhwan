using CoreKit.Access.Abstractions;
using CoreKit.Access.Constants;
using CoreKit.Access.Extensions;
using CoreKit.Tenant.Extensions;
using CoreKit.Tenant.Integration;
using CoreKit.Tenant.Resolution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using IPermissionChecker = CoreKit.Access.Abstractions.IPermissionChecker;

namespace CoreKit.Tenant.Tests;

public sealed class TenantAccessAuthorizationTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly TenantDbContext _tenantDb;
    private readonly CurrentTenant _currentTenant;
    private readonly TestAccessCurrentUser _currentUser;
    private readonly SqliteConnection _sharedConnection;

    public TenantAccessAuthorizationTests()
    {
        _sharedConnection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _sharedConnection.Open();

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

        _currentUser = new TestAccessCurrentUser(Guid.NewGuid(), true);
        services.AddScoped<IAccessCurrentUser>(_ => _currentUser);

        services.AddTenant(config, options => options.UseSqlite(_sharedConnection));
        services.AddAccess(config, options => options.UseSqlite(_sharedConnection));

        _provider = services.BuildServiceProvider();

        _tenantDb = _provider.GetRequiredService<TenantDbContext>();
        _currentTenant = _provider.GetRequiredService<CurrentTenant>();

        _tenantDb.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _tenantDb.Dispose();
        _provider.Dispose();
        _sharedConnection.Dispose();
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsFalse_WhenUserIsNotAMember()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentTenant.Set(tenantId);
        _currentUser.UserId = userId;

        var checker = _provider.GetRequiredService<IPermissionChecker>();

        var hasPermission = await checker.HasPermissionAsync("tenants.read");

        Assert.False(hasPermission);
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsFalse_WhenNoScopeResolved()
    {
        _currentUser.UserId = Guid.NewGuid();

        var checker = _provider.GetRequiredService<IPermissionChecker>();

        var hasPermission = await checker.HasPermissionAsync("tenants.read");

        Assert.False(hasPermission);
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsFalse_WhenUserNotAuthenticated()
    {
        var tenantId = Guid.NewGuid();

        _currentTenant.Set(tenantId);
        _currentUser.UserId = null;

        var checker = _provider.GetRequiredService<CoreKit.Access.Abstractions.IPermissionChecker>();

        var hasPermission = await checker.HasPermissionAsync("tenants.read");

        Assert.False(hasPermission);
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsFalse_WhenTenantOwnerIsNotAccessMember()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentTenant.Set(tenantId);
        _currentUser.UserId = userId;

        _tenantDb.Tenants.Add(new CoreKit.Tenant.Entities.TenantEntity
        {
            Id = tenantId,
            Name = "Test Tenant",
            Slug = "test-tenant",
            Status = CoreKit.Tenant.Entities.TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            StatusChangedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });
        _tenantDb.Members.Add(new CoreKit.Tenant.Entities.TenantMember
        {
            TenantId = tenantId,
            UserId = userId,
            IsOwner = true,
            IsDefault = true,
            JoinedAt = DateTime.UtcNow
        });
        _tenantDb.SaveChanges();

        var checker = _provider.GetRequiredService<CoreKit.Access.Abstractions.IPermissionChecker>();

        var hasPermission = await checker.HasPermissionAsync("tenants.read");

        Assert.False(hasPermission);
    }

    private sealed class TestAccessCurrentUser : IAccessCurrentUser
    {
        public TestAccessCurrentUser(Guid? userId, bool isAuthenticated)
        {
            UserId = userId;
            IsAuthenticated = isAuthenticated;
        }

        public Guid? UserId { get; set; }
        public bool IsAuthenticated { get; set; }
    }
}
