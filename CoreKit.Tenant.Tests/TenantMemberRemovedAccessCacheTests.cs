using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

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
        services.AddTenant(config, options => options.UseSqlite(_tenantConnection));
        services.AddAccess(config, options => options.UseSqlite(_accessConnection));
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
        using var s = _provider.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<Access>();
        db.Database.EnsureCreated();

        var role = new AccessRole
        {
            Id = Guid.NewGuid(),
            ScopeType = scope.Type,
            ScopeId = scope.Id,
            Name = name,
            NormalizedName = AccessValidation.NormalizeName(name),
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        foreach (var permission in permissions)
            role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, Permission = permission });

        db.Roles.Add(role);
        db.SaveChange();
        return role;
    }

    private void SeedTenantMembers(Guid tenantId, Guid owner, Guid member)
    {
        using var s = _provider.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<Tenant>();
        db.Database.EnsureCreated();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            Slug = "acme",
            Status = Active,
            CreatedAt = DateTime.UtcNow,
            StatusChangedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });
        db.Members.Add(new Member { TenantId = tenantId, UserId = owner, Owner = true, Default = true, JoinedAt = DateTime.UtcNow, Generation = 1 });
        db.Members.Add(new Member { TenantId = tenantId, UserId = member, Owner = false, Default = false, JoinedAt = DateTime.UtcNow, Generation = 1 });
        db.SaveChange();
    }
    private void SeedAccessAssignment(Guid tenantId, Guid member, AccessRole role, long generation = 1)
    {
        using var s = _provider.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<Access>();
        db.Database.EnsureCreated();

        db.Assignments.Add(new Assignment
        {
            RoleId = role.Id,
            UserId = member,
            ScopeType = AccessScope.TenantType,
            ScopeId = tenantId,
            Generation = generation,
            AssignedAt = DateTime.UtcNow
        });
        db.SaveChange();
    }

    private int CountAccessAssignments(Guid tenantId, Guid member)
    {
        using var s = _provider.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<Access>();
        return db.Assignments.Count(a => a.TenantId == tenantId && a.UserId == member);
    }

    private EffectiveAccess GetFreshAccess(Guid tenantId, Guid member)
    {
        using var s = _provider.CreateScope();
        var access = s.ServiceProvider.GetRequiredService<IAccess>();
        return access.GetAccessAsync(member, TenantScope(tenantId)).GetAwaiter().GetResult();
    }

    private void RemoveMemberAsyncFromTenant(Guid tenantId, Guid member)
    {
        using var s = _provider.CreateScope();
        var membership = s.ServiceProvider.GetRequiredService<ITenantMembershipService>();
        using var tenantCtx = s.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);

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
}
