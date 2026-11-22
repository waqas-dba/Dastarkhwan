using CoreKit.Access.Abstractions;
using CoreKit.Access.Extensions;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Constants;
using CoreKit.Tenant.Extensions;
using Dastarkhwan.Integration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CoreKit.Access.Entities;
using CoreKit.Access.Persistence;
using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Persistence;
using Xunit;

namespace CoreKit.Tenant.Tests;

/// <summary>
/// When a person leaves a tenant, Access must forget their tenant-scoped permissions. These tests run
/// the real Tenant membership service over a real DI graph (so the event handler registered by AddTenant
/// actually fires) and assert on Access state afterwards. They assert what a fresh authorization read
/// returns, not that an invalidation method was called.
/// </summary>
public sealed class TenantMemberRemovedAccessCacheTests : IDisposable
{
    private readonly SqliteConnection _tenantConnection;
    private readonly SqliteConnection _accessConnection;
    private readonly ServiceProvider _provider;

    public TenantMemberRemovedAccessCacheTests()
    {
        _tenantConnection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _tenantConnection.Open();
        _accessConnection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _accessConnection.Open();

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
        services.AddLogging();
        services.AddTenant(config, options => options.UseSqlite(_tenantConnection));
        services.AddAccess(config, options => options.UseSqlite(_accessConnection));
        services.AddDastarkhwanIntegration();

        // Override the permission checker for tests to grant all permissions to the test user
        services.AddScoped<ITenantPermissionChecker>(sp => new FakeTenantPermissionChecker(
            TenantPermissionNames.All.Select(p => p.Name).ToArray()));

        _provider = services.BuildServiceProvider(validateScopes: true);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _tenantConnection.Dispose();
        _accessConnection.Dispose();
    }

    private static AccessScope TenantScope(Guid tenantId) => AccessScope.ForTenant(tenantId);

    private AccessRole AddRole(AccessScope scope, string name, string[] permissions)
    {
        using var scope_ = _provider.CreateScope();
        var db = scope_.ServiceProvider.GetRequiredService<AccessDbContext>();
        db.Database.EnsureCreated();

        var role = new AccessRole
        {
            Id = Guid.NewGuid(),
            ScopeType = scope.Type,
            ScopeId = scope.Id,
            Name = name,
            NormalizedName = name.Trim().ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        foreach (var permission in permissions)
            role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, Permission = permission });

        db.Roles.Add(role);
        db.SaveChanges();
        return role;
    }

    private void SeedTenantMembers(Guid tenantId, Guid owner, Guid member)
    {
        using var scope_ = _provider.CreateScope();
        var db = scope_.ServiceProvider.GetRequiredService<TenantDbContext>();
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
            UserId = owner,
            IsOwner = true,
            IsDefault = true,
            JoinedAt = DateTime.UtcNow,
            MembershipGeneration = 1
        });
        db.Members.Add(new TenantMember
        {
            TenantId = tenantId,
            UserId = member,
            IsOwner = false,
            IsDefault = false,
            JoinedAt = DateTime.UtcNow,
            MembershipGeneration = 1
        });
        db.SaveChanges();
    }

    private void SeedAccessAssignment(Guid tenantId, Guid member, AccessRole role, long generation = 1)
    {
        using var scope_ = _provider.CreateScope();
        var db = scope_.ServiceProvider.GetRequiredService<AccessDbContext>();
        db.Database.EnsureCreated();

        db.Assignments.Add(new AccessAssignment
        {
            RoleId = role.Id,
            UserId = member,
            ScopeType = AccessScope.TenantType,
            ScopeId = tenantId,
            MembershipGeneration = generation,
            AssignedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private int CountAccessAssignments(Guid tenantId, Guid member)
    {
        using var scope_ = _provider.CreateScope();
        var db = scope_.ServiceProvider.GetRequiredService<AccessDbContext>();
        return db.Assignments.Count(
            a => a.ScopeType == AccessScope.TenantType && a.ScopeId == tenantId && a.UserId == member);
    }

    private EffectiveAccess GetFreshAccess(Guid tenantId, Guid member)
    {
        using var scope_ = _provider.CreateScope();
        var access = scope_.ServiceProvider.GetRequiredService<IAccessService>();
        return access.GetAccessAsync(member, TenantScope(tenantId)).GetAwaiter().GetResult();
    }

    private void RemoveMemberAsyncFromTenant(Guid tenantId, Guid member)
    {
        using var scope_ = _provider.CreateScope();
        var membership = scope_.ServiceProvider.GetRequiredService<ITenantMembershipService>();
        using var tenantCtx = scope_.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);

        var result = membership.RemoveMemberAsync(tenantId, member).GetAwaiter().GetResult();
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public void RemoveMemberAsync_RemovesTheUsersAccessRoleAssignments()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var role = AddRole(TenantScope(tenantId), "Manager", new[] { "sales.create" });

        SeedTenantMembers(tenantId, owner, member);
        SeedAccessAssignment(tenantId, member, role);
        Assert.Equal(1, CountAccessAssignments(tenantId, member));

        RemoveMemberAsyncFromTenant(tenantId, member);

        Assert.Equal(0, CountAccessAssignments(tenantId, member));
    }

    [Fact]
    public void RemoveMemberAsync_RemovedUserHasNoEffectiveAccessAfterwards()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var role = AddRole(TenantScope(tenantId), "Manager", new[] { "sales.create" });

        SeedTenantMembers(tenantId, owner, member);
        SeedAccessAssignment(tenantId, member, role);
        Assert.True(GetFreshAccess(tenantId, member).Has("sales.create"));

        RemoveMemberAsyncFromTenant(tenantId, member);

        var access = GetFreshAccess(tenantId, member);
        Assert.False(access.IsMember);
        Assert.False(access.Has("sales.create"));
    }

    private sealed class FakeTenantPermissionChecker : ITenantPermissionChecker
    {
        private readonly ISet<string> _permissions = new HashSet<string>(StringComparer.Ordinal);

        public FakeTenantPermissionChecker(params string[] permissions)
        {
            foreach (var permission in permissions)
                _permissions.Add(permission);
        }

        public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
            => Task.FromResult(_permissions.Contains(permission));

        public Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default)
            => Task.FromResult(_permissions.Contains(permission));

        public Task<bool> HasFreshPermissionAsync(string permission, CancellationToken ct = default)
            => Task.FromResult(_permissions.Contains(permission));
    }
}
