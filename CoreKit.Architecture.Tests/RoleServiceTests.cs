using CoreKit.Architecture.Tests.Support;

namespace CoreKit.Access.Tests;

public sealed class AccessRoleServiceReadTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private AccessRoleService Svc => _env.CreateRoleService();

    [Fact]
    public async Task ListPermissions_ShowsTheCatalog()
    {
        _env.SignInOwner();

        var permissions = ResultAssert.Ok(await Svc.ListPermissionsAsync());

        Assert.Contains(permissions, p => p.Name == "sales.create" && p.Description == "Ring up a sale");
        Assert.Contains(permissions, p => p.Name == AccessPermissionNames.RolesManage);
    }

    [Fact]
    public async Task EveryReadNeedsThePermission()
    {
        _env.SignInAs("sales.create");
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(await Svc.ListPermissionsAsync(), AccessErrorKind.Forbidden, "access.denied");
        ResultAssert.Fails(await Svc.ListAsync(), AccessErrorKind.Forbidden, "access.denied");
        ResultAssert.Fails(await Svc.GetAsync(role.Id), AccessErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task CallsWithoutAScopeOrUserAreRefused()
    {
        ResultAssert.Fails(await Svc.ListAsync(), AccessErrorKind.Unauthorized);

        _env.SignInOwner();
        _env.ScopeContext.Current = null;
        ResultAssert.Fails(await Svc.ListAsync(), AccessErrorKind.Validation, "access.scope_required");
    }

    [Fact]
    public async Task List_ShowsOnlyThisScopesRoles_SortedByName_WithMemberCounts()
    {
        _env.SignInOwner();
        var zeta = _env.AddRole("Zeta", new[] { "sales.create" });
        var alpha = _env.AddRole("Alpha");
        _env.AddRole("Foreign", scope: AccessScope.ForTenant(Guid.NewGuid()));
        _env.AddMember(roles: new[] { zeta });
        _env.AddMember(roles: new[] { zeta });

        var roles = ResultAssert.Ok(await Svc.ListAsync());

        Assert.Equal(new[] { "Alpha", "Zeta" }, roles.Select(r => r.Name));
        Assert.Equal(2, roles.Single(r => r.Name == "Zeta").MemberCount);
        Assert.Equal(0, roles.Single(r => r.Id == alpha.Id).MemberCount);
        Assert.Equal(new[] { "sales.create" }, roles.Single(r => r.Name == "Zeta").Permissions);
    }

    [Fact]
    public async Task Get_ReturnsTheRole_ButNeverOneFromAnotherScope()
    {
        _env.SignInOwner();
        var own = _env.AddRole("Cashier", new[] { "sales.create" });
        var foreign = _env.AddRole("Foreign", scope: AccessScope.ForTenant(Guid.NewGuid()));

        Assert.Equal("Cashier", ResultAssert.Ok(await Svc.GetAsync(own.Id)).Name);
        ResultAssert.Fails(await Svc.GetAsync(foreign.Id), AccessErrorKind.NotFound);
        ResultAssert.Fails(await Svc.GetAsync(Guid.NewGuid()), AccessErrorKind.NotFound);
    }
}

public sealed class AccessRoleServiceCreateTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private AccessRoleService Svc => _env.CreateRoleService();

    [Fact]
    public async Task Create_SavesTheRoleInTheCurrentScope()
    {
        _env.SignInOwner();

        var dto = ResultAssert.Ok(await Svc.CreateAsync(new CreateAccessRoleRequest
        {
            Name = "  Cashier ",
            Description = " Rings up sales ",
            Permissions = { "Sales.Create", "products.view", "sales.create" }
        }));

        Assert.Equal("Cashier", dto.Name);
        Assert.Equal("Rings up sales", dto.Description);
        Assert.Equal(new[] { "products.view", "sales.create" }, dto.Permissions);
        Assert.Equal(0, dto.MemberCount);
        Assert.Equal(_env.UtcNow, dto.CreatedAt);

        var row = _env.Read(db => db.Roles.AsNoTracking().Include(r => r.Permissions).Single(r => r.Id == dto.Id));
        Assert.Equal(_env.Scope.Type, row.ScopeType);
        Assert.Equal(_env.Scope.Id, row.ScopeId);
        Assert.Equal("CASHIER", row.NormalizedName);
        Assert.Equal(2, row.Permissions.Count);
        Assert.NotEqual(Guid.Empty, row.ConcurrencyStamp);
    }

    [Fact]
    public async Task Create_WritesAnAuditEntryWithTheActor()
    {
        var owner = _env.SignInOwner();

        await Svc.CreateAsync(new CreateAccessRoleRequest { Name = "Cashier" });

        var entry = _env.Read(db => db.AuditEntries.Single(a => a.Action == AccessAuditActions.RoleCreated));
        Assert.Equal(owner, entry.ActorUserId);
        Assert.Equal(_env.Scope.Id, entry.ScopeId);
        Assert.Contains("Cashier", entry.Details);
    }

    [Fact]
    public async Task Create_NeedsTheManagePermission()
    {
        _env.SignInAs(AccessPermissionNames.RolesRead);

        ResultAssert.Fails(
            await Svc.CreateAsync(new CreateAccessRoleRequest { Name = "Cashier" }), AccessErrorKind.Forbidden, "access.denied");
        Assert.Empty(_env.Read(db => db.Roles.ToList()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RequiresAName(string name)
    {
        _env.SignInOwner();

        ResultAssert.Fails(await Svc.CreateAsync(new CreateAccessRoleRequest { Name = name }), AccessErrorKind.Validation);
    }

    [Fact]
    public async Task Create_EnforcesTheTextLimits()
    {
        _env.SignInOwner();

        ResultAssert.Fails(
            await Svc.CreateAsync(new CreateAccessRoleRequest { Name = new string('a', 101) }), AccessErrorKind.Validation);
        ResultAssert.Fails(
            await Svc.CreateAsync(new CreateAccessRoleRequest { Name = "Ok", Description = new string('a', 501) }),
            AccessErrorKind.Validation);
    }

    [Fact]
    public async Task Create_RejectsAPermissionNobodyDeclared()
    {
        _env.SignInOwner();

        ResultAssert.Fails(
            await Svc.CreateAsync(new CreateAccessRoleRequest { Name = "Odd", Permissions = { "made.up" } }),
            AccessErrorKind.Validation);
    }

    [Fact]
    public async Task Create_RejectsADuplicateName_IgnoringCase_ButOnlyWithinTheScope()
    {
        _env.SignInOwner();
        _env.AddRole("Cashier");
        _env.AddRole("Waiter", scope: AccessScope.ForTenant(Guid.NewGuid()));

        ResultAssert.Fails(await Svc.CreateAsync(new CreateAccessRoleRequest { Name = "CASHIER" }), AccessErrorKind.Conflict);
        ResultAssert.Ok(await Svc.CreateAsync(new CreateAccessRoleRequest { Name = "Waiter" }));
    }

    [Fact]
    public async Task Create_EnforcesTheRoleLimit()
    {
        using var env = new TestEnv(new AccessOptions { MaxRolesPerScope = 1 });
        env.SignInOwner();
        env.AddRole("First");

        ResultAssert.Fails(
            await env.CreateRoleService().CreateAsync(new CreateAccessRoleRequest { Name = "Second" }), AccessErrorKind.Conflict);
    }

    [Fact]
    public async Task Create_CannotGrantPermissionsTheCallerLacks()
    {
        _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");

        ResultAssert.Fails(
            await Svc.CreateAsync(new CreateAccessRoleRequest { Name = "Boss", Permissions = { "sales.create", "sales.refund" } }),
            AccessErrorKind.Forbidden, "access.escalation");
        ResultAssert.Ok(
            await Svc.CreateAsync(new CreateAccessRoleRequest { Name = "Seller", Permissions = { "sales.create" } }));
    }

    [Fact]
    public async Task Create_AFailedRequestLeavesNoAuditEntry()
    {
        _env.SignInOwner();

        await Svc.CreateAsync(new CreateAccessRoleRequest { Name = "" });

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }
}

public sealed class AccessRoleServiceChangeTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private AccessRoleService Svc => _env.CreateRoleService();

    [Fact]
    public async Task Update_ChangesNameAndDescription()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        var dto = ResultAssert.Ok(await Svc.UpdateAsync(
            role.Id, new UpdateAccessRoleRequest { Name = " Till operator ", Description = "At the till" }));

        Assert.Equal("Till operator", dto.Name);
        Assert.Equal("At the till", dto.Description);
        Assert.Equal(_env.UtcNow, dto.UpdatedAt);

        var row = _env.Read(db => db.Roles.AsNoTracking().Single(r => r.Id == role.Id));
        Assert.Equal("TILL OPERATOR", row.NormalizedName);
        Assert.NotEqual(role.ConcurrencyStamp, row.ConcurrencyStamp);
    }

    [Fact]
    public async Task Update_AnEmptyDescriptionClearsIt_AndNullLeavesItAlone()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");
        _env.Seed(db => db.Roles.Single(r => r.Id == role.Id).Description = "Keep me");

        await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { Name = "Teller" });
        Assert.Equal("Keep me", _env.Read(db => db.Roles.Single(r => r.Id == role.Id)).Description);

        await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { Description = "" });
        Assert.Null(_env.Read(db => db.Roles.Single(r => r.Id == role.Id)).Description);
    }

    [Fact]
    public async Task Update_ACaseOnlyRename_IsAllowed()
    {
        _env.SignInOwner();
        var role = _env.AddRole("cashier");

        var dto = ResultAssert.Ok(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { Name = "Cashier" }));

        Assert.Equal("Cashier", dto.Name);
    }

    [Fact]
    public async Task Update_NothingChanged_IsASilentSuccess()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Ok(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { Name = "Cashier" }));

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }

    [Fact]
    public async Task Update_RefusesANameAnotherRoleHas_AndChangesNothing()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");
        _env.AddRole("Waiter");

        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { Name = "WAITER" }), AccessErrorKind.Conflict);
        Assert.Equal("Cashier", _env.Read(db => db.Roles.Single(r => r.Id == role.Id)).Name);
    }

    [Fact]
    public async Task Update_AValidationFailureChangesNothing()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(
            await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { Name = "Renamed", Description = new string('a', 501) }),
            AccessErrorKind.Validation);
        Assert.Equal("Cashier", _env.Read(db => db.Roles.Single(r => r.Id == role.Id)).Name);
    }

    [Fact]
    public async Task Update_UnknownRoleAndOtherScopesRolesAreNotFound()
    {
        _env.SignInOwner();
        var foreign = _env.AddRole("Foreign", scope: AccessScope.ForTenant(Guid.NewGuid()));

        ResultAssert.Fails(await Svc.UpdateAsync(Guid.NewGuid(), new UpdateAccessRoleRequest { Name = "x" }), AccessErrorKind.NotFound);
        ResultAssert.Fails(await Svc.UpdateAsync(foreign.Id, new UpdateAccessRoleRequest { Name = "x" }), AccessErrorKind.NotFound);
    }

    [Fact]
    public async Task Update_NeedsTheManagePermission_AndCannotTouchAStrongerRole()
    {
        var role = _env.AddRole("Boss", new[] { "sales.refund" });

        _env.SignInAs(AccessPermissionNames.RolesRead);
        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { Name = "x" }), AccessErrorKind.Forbidden, "access.denied");

        _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");
        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { Name = "x" }), AccessErrorKind.Forbidden, "access.escalation");
    }

    [Fact]
    public async Task Update_TakesEffectAtOnceForPeopleWhoHoldTheRole()
    {
        var owner = _env.SignInOwner();
        var role = _env.AddRole("Old name", new[] { "sales.create" });
        var holder = _env.AddMember(roles: new[] { role });
        Assert.Equal(new[] { "Old name" }, (await _env.Access.GetAccessAsync(holder, _env.Scope)).Roles);

        await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { Name = "New name" });

        Assert.Equal(new[] { "New name" }, (await _env.Access.GetAccessAsync(holder, _env.Scope)).Roles);
        Assert.NotEqual(Guid.Empty, owner);
    }

    [Fact]
    public async Task SetPermissions_AddsAndRemoves_AndAuditsBoth()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create", "products.view" });

        var dto = ResultAssert.Ok(await Svc.SetPermissionsAsync(
            role.Id, new SetAccessRolePermissionsRequest { Permissions = { "products.view", "sales.refund" } }));

        Assert.Equal(new[] { "products.view", "sales.refund" }, dto.Permissions);

        var entry = _env.Read(db => db.AuditEntries.Single(a => a.Action == AccessAuditActions.RolePermissionsChanged));
        Assert.Contains("added: sales.refund", entry.Details);
        Assert.Contains("removed: sales.create", entry.Details);
    }

    [Fact]
    public async Task SetPermissions_CanClearEveryPermission()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        var dto = ResultAssert.Ok(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest()));

        Assert.Empty(dto.Permissions);
        Assert.Empty(_env.Read(db => db.RolePermissions.ToList()));
    }

    [Fact]
    public async Task SetPermissions_NothingChanged_IsASilentSuccess()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Ok(await Svc.SetPermissionsAsync(
            role.Id, new SetAccessRolePermissionsRequest { Permissions = { "sales.create" } }));

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }

    [Fact]
    public async Task SetPermissions_RejectsUnknownPermissions_AndUnknownRoles()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(
            await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest { Permissions = { "made.up" } }),
            AccessErrorKind.Validation);
        ResultAssert.Fails(
            await Svc.SetPermissionsAsync(Guid.NewGuid(), new SetAccessRolePermissionsRequest()), AccessErrorKind.NotFound);
    }

    [Fact]
    public async Task SetPermissions_CannotAddAPermissionTheCallerLacks()
    {
        _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Fails(
            await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest { Permissions = { "sales.create", "sales.refund" } }),
            AccessErrorKind.Forbidden, "access.escalation");
    }

    [Fact]
    public async Task SetPermissions_CannotStripARoleThatIsStrongerThanTheCaller()
    {
        _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");
        var role = _env.AddRole("Boss", new[] { "sales.create", "sales.refund" });

        ResultAssert.Fails(
            await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest { Permissions = { "sales.create" } }),
            AccessErrorKind.Forbidden, "access.escalation");
        Assert.Equal(2, _env.Read(db => db.RolePermissions.Count(p => p.RoleId == role.Id)));
    }

    [Fact]
    public async Task SetPermissions_TakesEffectAtOnceForPeopleWhoHoldTheRole()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });
        var holder = _env.AddMember(roles: new[] { role });
        Assert.True((await _env.Access.GetAccessAsync(holder, _env.Scope)).Has("sales.create"));

        await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest { Permissions = { "products.view" } });

        var after = await _env.Access.GetAccessAsync(holder, _env.Scope);
        Assert.False(after.Has("sales.create"));
        Assert.True(after.Has("products.view"));
    }

    [Fact]
    public async Task Delete_RemovesTheRoleAndItsPermissions()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Ok(await Svc.DeleteAsync(role.Id));

        Assert.Empty(_env.Read(db => db.Roles.ToList()));
        Assert.Empty(_env.Read(db => db.RolePermissions.ToList()));
        Assert.Contains(_env.Read(db => db.AuditEntries.ToList()), a => a.Action == AccessAuditActions.RoleDeleted);
    }

    [Fact]
    public async Task Delete_RefusesARoleSomeoneStillHolds()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");
        _env.AddMember(roles: new[] { role });

        ResultAssert.Fails(await Svc.DeleteAsync(role.Id), AccessErrorKind.Conflict);
        Assert.Single(_env.Read(db => db.Roles.ToList()));
    }

    [Fact]
    public async Task Delete_UnknownRoleIsNotFound_AndAStrongerRoleCannotBeDeleted()
    {
        _env.SignInAs(AccessPermissionNames.RolesManage);
        var boss = _env.AddRole("Boss", new[] { "sales.refund" });

        ResultAssert.Fails(await Svc.DeleteAsync(Guid.NewGuid()), AccessErrorKind.NotFound);
        ResultAssert.Fails(await Svc.DeleteAsync(boss.Id), AccessErrorKind.Forbidden, "access.escalation");
    }

    [Fact]
    public async Task Delete_NeedsTheManagePermission()
    {
        _env.SignInAs(AccessPermissionNames.RolesRead);
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(await Svc.DeleteAsync(role.Id), AccessErrorKind.Forbidden, "access.denied");
    }
}