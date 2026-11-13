using CoreKit.Access.Abstractions;
using CoreKit.Access.Constants;
using CoreKit.Access.Entities;
using CoreKit.Access.Extensions;
using CoreKit.Access.Persistence;
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

        // EnsureCreated() works for Tenant on the shared connection. Access cannot use
        // EnsureCreated() here because both contexts share the default
        // __EFMigrationsHistory table in SQLite test mode (the Npgsql-specific history
        // table configuration is bypassed when UseSqlite is passed directly), so the
        // second EnsureCreated() is a no-op. Access tables are created directly instead.
        _tenantDb.Database.EnsureCreated();

        using var cmd = _sharedConnection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""ACCESS_Roles"" (
                ""Id"" TEXT NOT NULL,
                ""ScopeType"" TEXT NOT NULL,
                ""ScopeId"" TEXT NOT NULL,
                ""Name"" TEXT NOT NULL,
                ""NormalizedName"" TEXT NOT NULL,
                ""Description"" TEXT,
                ""CreatedAt"" TEXT NOT NULL,
                ""UpdatedAt"" TEXT,
                ""ConcurrencyStamp"" TEXT NOT NULL,
                CONSTRAINT ""PK_ACCESS_Roles"" PRIMARY KEY (""Id"")
            );
            CREATE TABLE IF NOT EXISTS ""ACCESS_RolePermissions"" (
                ""RoleId"" TEXT NOT NULL,
                ""Permission"" TEXT NOT NULL,
                CONSTRAINT ""PK_ACCESS_RolePermissions"" PRIMARY KEY (""RoleId"", ""Permission"")
            );
            CREATE TABLE IF NOT EXISTS ""ACCESS_Assignments"" (
                ""RoleId"" TEXT NOT NULL,
                ""UserId"" TEXT NOT NULL,
                ""ScopeType"" TEXT NOT NULL,
                ""ScopeId"" TEXT NOT NULL,
                ""MembershipGeneration"" INTEGER NOT NULL,
                ""AssignedAt"" TEXT NOT NULL,
                ""AssignedBy"" TEXT,
                CONSTRAINT ""PK_ACCESS_Assignments"" PRIMARY KEY (""RoleId"", ""UserId"")
            );
        ";
        cmd.ExecuteNonQuery();
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
    public async Task HasPermissionAsync_ReturnsFalse_WhenUserIdSetButNotAuthenticated()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentTenant.Set(tenantId);
        _currentUser.UserId = userId;
        _currentUser.IsAuthenticated = false;

        var checker = _provider.GetRequiredService<IPermissionChecker>();

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

    [Fact]
    public async Task HasPermissionAsync_ReturnsTrue_WhenAuthenticatedUserHasPermission()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentTenant.Set(tenantId);
        _currentUser.UserId = userId;
        _currentUser.IsAuthenticated = true;

        // Tenant membership (via Tenant DB) makes the user a scope member.
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
            IsOwner = false,
            IsDefault = true,
            JoinedAt = DateTime.UtcNow
        });
        _tenantDb.SaveChanges();

        // Access role + assignment grant tenants.read in the tenant scope.
        using var accessDb = _provider.GetRequiredService<AccessDbContext>();
        var roleId = Guid.NewGuid();
        accessDb.Roles.Add(new AccessRole
        {
            Id = roleId,
            ScopeType = AccessScope.TenantType,
            ScopeId = tenantId,
            Name = "Reader",
            NormalizedName = "READER",
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            Permissions =
            {
                new AccessRolePermission
                {
                    RoleId = roleId,
                    Permission = "tenants.read"
                }
            }
        });
        accessDb.Assignments.Add(new AccessAssignment
        {
            RoleId = roleId,
            UserId = userId,
            ScopeType = AccessScope.TenantType,
            ScopeId = tenantId,
            MembershipGeneration = 1,
            AssignedAt = DateTime.UtcNow
        });
        accessDb.SaveChanges();

        var checker = _provider.GetRequiredService<IPermissionChecker>();

        var hasPermission = await checker.HasPermissionAsync("tenants.read");

        Assert.True(hasPermission);
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
