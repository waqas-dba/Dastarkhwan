using CoreKit.Architecture.Tests.Support;

namespace CoreKit.Access.Tests;

public sealed class AccessMaintenanceTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private AccessMaintenance Svc => _env.CreateMaintenance();

    [Fact]
    public async Task RemoveUser_DropsTheirRoles_AndNobodyElses()
    {
        var cashier = _env.AddRole("Cashier", new[] { "sales.create" });
        var leaver = _env.AddMember(roles: new[] { cashier });
        var stayer = _env.AddMember(roles: new[] { cashier });
        Assert.True((await _env.Access.GetAccessAsync(leaver, _env.Scope)).Has("sales.create"));

        await Svc.RemoveUserAsync(_env.Scope, leaver);

        Assert.Equal(stayer, Assert.Single(_env.Read(db => db.Assignments.ToList())).UserId);
        Assert.Contains(_env.Read(db => db.AuditEntries.ToList()), a => a.Action == AccessAuditActions.UserRemoved);
        Assert.False((await _env.Access.GetAccessAsync(leaver, _env.Scope)).Has("sales.create"));
    }

    [Fact]
    public async Task RemoveUser_TouchesOnlyThatScope()
    {
        var other = AccessScope.ForTenant(Guid.NewGuid());
        var user = Guid.NewGuid();
        var here = _env.AddRole("Here");
        var there = _env.AddRole("There", scope: other);
        _env.Assign(user, here);
        _env.Assign(user, there);

        await Svc.RemoveUserAsync(_env.Scope, user);

        Assert.Equal(there.Id, Assert.Single(_env.Read(db => db.Assignments.ToList())).RoleId);
    }

    [Fact]
    public async Task RemoveUser_IgnoresEmptyInput()
    {
        await Svc.RemoveUserAsync(_env.Scope, Guid.Empty);
        await Svc.RemoveUserAsync(new AccessScope("", Guid.NewGuid()), Guid.NewGuid());

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }

    [Fact]
    public async Task RemoveScope_DeletesEverythingOfThatScope_AndKeepsTheHistory()
    {
        var other = AccessScope.ForTenant(Guid.NewGuid());
        var mine = _env.AddRole("Mine", new[] { "sales.create" });
        var theirs = _env.AddRole("Theirs", new[] { "sales.create" }, other);
        _env.AddMember(roles: new[] { mine });
        _env.Assign(Guid.NewGuid(), theirs);
        _env.Seed(db => db.AuditEntries.Add(new AccessAuditEntry
        {
            Id = Guid.NewGuid(),
            ScopeType = _env.Scope.Type,
            ScopeId = _env.Scope.Id,
            Action = "earlier",
            OccurredAt = _env.UtcNow
        }));

        await Svc.RemoveScopeAsync(_env.Scope);

        Assert.Equal(theirs.Id, Assert.Single(_env.Read(db => db.Roles.ToList())).Id);
        Assert.Equal(1, _env.Read(db => db.RolePermissions.Count()));
        Assert.Equal(theirs.Id, Assert.Single(_env.Read(db => db.Assignments.ToList())).RoleId);

        var actions = _env.Read(db => db.AuditEntries.Where(a => a.ScopeId == _env.Scope.Id).Select(a => a.Action).ToList());
        Assert.Contains("earlier", actions);
        Assert.Contains(AccessAuditActions.ScopeRemoved, actions);
    }

    [Fact]
    public async Task RemoveScope_ForgetsRememberedPermissions()
    {
        var role = _env.AddRole("Cashier", new[] { "sales.create" });
        var user = _env.AddMember(roles: new[] { role });
        Assert.True((await _env.Access.GetAccessAsync(user, _env.Scope)).Has("sales.create"));

        await Svc.RemoveScopeAsync(_env.Scope);

        Assert.False((await _env.Access.GetAccessAsync(user, _env.Scope)).Has("sales.create"));
    }

    [Fact]
    public async Task Provision_CopiesTheTemplates_WithTheirPermissions()
    {
        await Svc.ProvisionScopeAsync(_env.Scope);

        var roles = _env.Read(db => db.Roles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.Name).ToList());

        Assert.Equal(new[] { "Cashier", "Manager" }, roles.Select(r => r.Name));
        Assert.Equal(new[] { "products.view", "sales.create" }, roles[0].Permissions.Select(p => p.Permission).Order());
        Assert.Equal(7, roles[1].Permissions.Count);
        Assert.All(roles, r => Assert.Equal(_env.Scope.Id, r.ScopeId));
        Assert.Contains(_env.Read(db => db.AuditEntries.ToList()), a => a.Action == AccessAuditActions.ScopeProvisioned);
    }

    [Fact]
    public async Task Provision_RunsOnlyOnce_AndNeverOverwritesExistingRoles()
    {
        await Svc.ProvisionScopeAsync(_env.Scope);
        await Svc.ProvisionScopeAsync(_env.Scope);

        Assert.Equal(2, _env.Read(db => db.Roles.Count()));

        using var env = new TestEnv();
        env.AddRole("My own role");
        await env.CreateMaintenance().ProvisionScopeAsync(env.Scope);

        Assert.Equal("My own role", Assert.Single(env.Read(db => db.Roles.ToList())).Name);
    }

    [Fact]
    public async Task Provision_NeedsTemplatesAndAValidScope()
    {
        using var env = new TestEnv(catalog: b => b.Add("sales.create", "x"));

        await env.CreateMaintenance().ProvisionScopeAsync(env.Scope);
        await Svc.ProvisionScopeAsync(new AccessScope("", Guid.NewGuid()));

        Assert.Empty(env.Read(db => db.Roles.ToList()));
        Assert.Empty(_env.Read(db => db.Roles.ToList()));
    }
}

public sealed class TenantEventHandlerTests : IDisposable
{
    private readonly TestEnv _env = new();
    private static readonly DateTime Moment = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task TenantCreated_ProvisionsTheTemplates()
    {
        await new TenantCreatedHandler(_env.CreateMaintenance()).HandleAsync(new TenantCreated(_env.Scope.Id, Moment));

        Assert.Equal(2, _env.Read(db => db.Roles.Count(r => r.ScopeId == _env.Scope.Id)));
    }

    [Fact]
    public async Task TenantMemberRemoved_DropsThatPersonsRoles()
    {
        var role = _env.AddRole("Cashier");
        var user = _env.AddMember(roles: new[] { role });

        await new TenantMemberRemovedHandler(_env.CreateMaintenance())
            .HandleAsync(new TenantMemberRemoved(_env.Scope.Id, user, Moment));

        Assert.Empty(_env.Read(db => db.Assignments.ToList()));
    }

    [Fact]
    public async Task TenantDeleted_RemovesTheTenantsRoles()
    {
        var role = _env.AddRole("Cashier", new[] { "sales.create" });
        _env.AddMember(roles: new[] { role });

        await new TenantDeletedHandler(_env.CreateMaintenance()).HandleAsync(new TenantDeleted(_env.Scope.Id, Moment));

        Assert.Empty(_env.Read(db => db.Roles.ToList()));
        Assert.Empty(_env.Read(db => db.Assignments.ToList()));
    }
}

public sealed class AccessAuditLogTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private async Task MakeSomeHistoryAsync()
    {
        var owner = _env.SignInOwner();
        var roles = _env.CreateRoleService();

        var cashier = ResultAssert.Ok(await roles.CreateAsync(new CreateAccessRoleRequest { Name = "Cashier" }));
        _env.Time.Advance(TimeSpan.FromMinutes(1));
        await roles.UpdateAsync(cashier.Id, new UpdateAccessRoleRequest { Name = "Teller" });
        _env.Time.Advance(TimeSpan.FromMinutes(1));
        await roles.DeleteAsync(cashier.Id);

        Assert.NotEqual(Guid.Empty, owner);
    }

    [Fact]
    public async Task List_ShowsNewestFirst_WithTheActor()
    {
        await MakeSomeHistoryAsync();

        var page = ResultAssert.Ok(await _env.CreateAuditLog().ListAsync());

        Assert.Equal(
            new[] { AccessAuditActions.RoleDeleted, AccessAuditActions.RoleUpdated, AccessAuditActions.RoleCreated },
            page.Items.Select(e => e.Action));
        Assert.Equal(3, page.TotalCount);
        Assert.All(page.Items, e => Assert.Equal(_env.User.UserId, e.ActorUserId));
    }

    [Fact]
    public async Task List_Pages()
    {
        await MakeSomeHistoryAsync();

        var second = ResultAssert.Ok(await _env.CreateAuditLog().ListAsync(page: 2, pageSize: 2));

        Assert.Equal(new[] { AccessAuditActions.RoleCreated }, second.Items.Select(e => e.Action));
        Assert.Equal(2, second.TotalPages);
    }

    [Fact]
    public async Task List_ClampsPaging()
    {
        _env.SignInOwner();

        var page = ResultAssert.Ok(await _env.CreateAuditLog().ListAsync(page: 0, pageSize: 100000));

        Assert.Equal(1, page.Page);
        Assert.Equal(200, page.PageSize);
    }

    [Fact]
    public async Task List_ShowsOnlyThisScopesHistory()
    {
        _env.SignInOwner();
        _env.Seed(db => db.AuditEntries.Add(new AccessAuditEntry
        {
            Id = Guid.NewGuid(),
            ScopeType = "tenant",
            ScopeId = Guid.NewGuid(),
            Action = "foreign",
            OccurredAt = _env.UtcNow
        }));

        var page = ResultAssert.Ok(await _env.CreateAuditLog().ListAsync());

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task List_NeedsThePermission()
    {
        _env.SignInAs(AccessPermissionNames.RolesRead);

        ResultAssert.Fails(await _env.CreateAuditLog().ListAsync(), AccessErrorKind.Forbidden, "access.denied");
    }
}