using Microsoft.Extensions.Caching.Memory;

namespace CoreKit.Access.Tests;

/// <summary>
/// Changing roles or assignments is decided on a person's permissions right now, not on a remembered copy.
/// </summary>
public sealed class FreshAuthorityTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task ManagingRoles_UsesCurrentAuthority_NotTheCache()
    {
        var manager = _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");

        // The manager's permissions are now remembered by the cache.
        Assert.True((await _env.Access.GetAccessAsync(manager, _env.Scope)).Has(AccessPermissionNames.RolesManage));

        // Their role is withdrawn directly in the database, so the cache has not heard about it.
        _env.Seed(db => db.Assignments.RemoveRange(db.Assignments.Where(a => a.UserId == manager)));

        var result = await _env.CreateRoleService().CreateAsync(new CreateAccessRoleRequest { Name = "Sneaky" });

        ResultAssert.Fails(result, AccessErrorKind.Forbidden, AccessErrors.Denied.Code);
        Assert.Empty(_env.Read(db => db.Roles.Where(r => r.Name == "Sneaky").ToList()));
    }

    [Fact]
    public async Task ManagingAssignments_UsesCurrentAuthority_NotTheCache()
    {
        var manager = _env.SignInAs(AccessPermissionNames.AssignmentsManage, "sales.create");
        var cashier = _env.AddRole("Cashier", new[] { "sales.create" });
        var staff = _env.AddMember();

        Assert.True((await _env.Access.GetAccessAsync(manager, _env.Scope)).Has(AccessPermissionNames.AssignmentsManage));

        _env.Seed(db => db.Assignments.RemoveRange(db.Assignments.Where(a => a.UserId == manager)));

        var result = await _env.CreateAssignmentService().SetRolesAsync(
            staff, new SetAccessUserRolesRequest { RoleIds = new List<Guid> { cashier.Id } });

        ResultAssert.Fails(result, AccessErrorKind.Forbidden, AccessErrors.Denied.Code);
        Assert.Empty(_env.Read(db => db.Assignments.Where(a => a.UserId == staff).ToList()));
    }

    [Fact]
    public async Task ReadingRoles_StillUsesTheCache()
    {
        _env.SignInAs(AccessPermissionNames.RolesRead);
        var service = _env.CreateRoleService();

        ResultAssert.Ok(await service.ListAsync());
        ResultAssert.Ok(await service.ListAsync());

        Assert.Equal(1, _env.Membership.Lookups);
    }
}

/// <summary>
/// A lookup that started before a role or assignment changed must not store its (stale) result.
/// </summary>
public sealed class AccessCacheStaleResultTests
{
    private readonly MemoryCache _memory = new(new MemoryCacheOptions());

    private AccessCache Cache() => new(_memory, Options.Create(new AccessOptions { CacheSeconds = 30 }));

    private static readonly AccessScope Scope = AccessScope.ForTenant(Guid.NewGuid());

    private static EffectiveAccess Access(params string[] permissions)
        => new(true, false, permissions.ToHashSet(), Array.Empty<string>());

    [Fact]
    public void ResultIsStored_WhenNothingChangedDuringTheLookup()
    {
        var cache = Cache();
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.Set(Scope, user, Access("a.b"), token);

        Assert.True(cache.TryGet(Scope, user, out var found));
        Assert.True(found.Has("a.b"));
    }

    [Fact]
    public void ResultIsNotStored_WhenTheScopeWasInvalidatedDuringTheLookup()
    {
        var cache = Cache();
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.InvalidateScope(Scope);
        cache.Set(Scope, user, Access("old.rights"), token);

        Assert.False(cache.TryGet(Scope, user, out _));
    }

    [Fact]
    public void ResultIsNotStored_WhenTheUserWasInvalidatedDuringTheLookup()
    {
        var cache = Cache();
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.InvalidateUser(Scope, user);
        cache.Set(Scope, user, Access("old.rights"), token);

        Assert.False(cache.TryGet(Scope, user, out _));
    }

    [Fact]
    public void InvalidatingOneUser_DoesNotBlockStoringForAnotherUser()
    {
        var cache = Cache();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var token = cache.Begin(Scope, second);
        cache.InvalidateUser(Scope, first);
        cache.Set(Scope, second, Access("a.b"), token);

        Assert.True(cache.TryGet(Scope, second, out _));
    }
}

public sealed class AccessAuditDetailTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task AuditDetails_HaveNoLineBreaksOrControlCharacters()
    {
        _env.Audit.Record(_env.Scope, "access.test", "line1\nline2\r\u0007end");

        Assert.Equal(SaveOutcome.Saved, await _env.UnitOfWork.SaveAsync());

        var entry = _env.Read(db => db.AuditEntries.Single());
        Assert.Equal("line1 line2  end", entry.Details);
    }

    [Fact]
    public async Task SetRoles_RecordsWhatWasAddedAndWhatWasRemoved()
    {
        _env.SignInOwner();
        var cashier = _env.AddRole("Cashier", new[] { "sales.create" });
        var stock = _env.AddRole("Stock", new[] { "products.manage" });
        var staff = _env.AddMember(roles: new[] { cashier });

        ResultAssert.Ok(await _env.CreateAssignmentService().SetRolesAsync(
            staff, new SetAccessUserRolesRequest { RoleIds = new List<Guid> { stock.Id } }));

        var entry = _env.Read(db => db.AuditEntries.Single(a => a.Action == AccessAuditActions.RolesAssigned));
        Assert.Contains("added: Stock", entry.Details);
        Assert.Contains("removed: Cashier", entry.Details);
    }
}
