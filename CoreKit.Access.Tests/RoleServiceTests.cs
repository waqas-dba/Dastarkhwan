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

        var roleCountBefore = _env.Read(db => db.Roles.Count());

        ResultAssert.Fails(
            await Svc.CreateAsync(new CreateAccessRoleRequest { Name = "Cashier" }), AccessErrorKind.Forbidden, "access.denied");
        Assert.Equal(roleCountBefore, _env.Read(db => db.Roles.Count()));
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
            role.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = role.ConcurrencyStamp, Name = " Till operator ", Description = "At the till" }));

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

        var dto = ResultAssert.Ok(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = role.ConcurrencyStamp, Name = "Teller" }));
        Assert.Equal("Keep me", _env.Read(db => db.Roles.Single(r => r.Id == role.Id)).Description);

        await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = dto.ConcurrencyStamp, Description = "" });
        Assert.Null(_env.Read(db => db.Roles.Single(r => r.Id == role.Id)).Description);
    }

    [Fact]
    public async Task Update_ACaseOnlyRename_IsAllowed()
    {
        _env.SignInOwner();
        var role = _env.AddRole("cashier");

        var dto = ResultAssert.Ok(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = role.ConcurrencyStamp, Name = "Cashier" }));

        Assert.Equal("Cashier", dto.Name);
    }

    [Fact]
    public async Task Update_NothingChanged_IsASilentSuccess()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Ok(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = role.ConcurrencyStamp, Name = "Cashier" }));

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }

    [Fact]
    public async Task Update_RefusesANameAnotherRoleHas_AndChangesNothing()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");
        _env.AddRole("Waiter");

        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = role.ConcurrencyStamp, Name = "WAITER" }), AccessErrorKind.Conflict);
        Assert.Equal("Cashier", _env.Read(db => db.Roles.Single(r => r.Id == role.Id)).Name);
    }

    [Fact]
    public async Task Update_AValidationFailureChangesNothing()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(
            await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = role.ConcurrencyStamp, Name = "Renamed", Description = new string('a', 501) }),
            AccessErrorKind.Validation);
        Assert.Equal("Cashier", _env.Read(db => db.Roles.Single(r => r.Id == role.Id)).Name);
    }

    [Fact]
    public async Task Update_UnknownRoleAndOtherScopesRolesAreNotFound()
    {
        _env.SignInOwner();
        var foreign = _env.AddRole("Foreign", scope: AccessScope.ForTenant(Guid.NewGuid()));

        ResultAssert.Fails(await Svc.UpdateAsync(Guid.NewGuid(), new UpdateAccessRoleRequest { ConcurrencyStamp = Guid.NewGuid(), Name = "x" }), AccessErrorKind.NotFound);
        ResultAssert.Fails(await Svc.UpdateAsync(foreign.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = Guid.NewGuid(), Name = "x" }), AccessErrorKind.NotFound);
    }

    [Fact]
    public async Task Update_NeedsTheManagePermission_AndCannotTouchAStrongerRole()
    {
        var role = _env.AddRole("Boss", new[] { "sales.refund" });

        _env.SignInAs(AccessPermissionNames.RolesRead);
        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = role.ConcurrencyStamp, Name = "x" }), AccessErrorKind.Forbidden, "access.denied");

        _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");
        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = role.ConcurrencyStamp, Name = "x" }), AccessErrorKind.Forbidden, "access.escalation");
    }

    [Fact]
    public async Task Update_TakesEffectAtOnceForPeopleWhoHoldTheRole()
    {
        var owner = _env.SignInOwner();
        var role = _env.AddRole("Old name", new[] { "sales.create" });
        var holder = _env.AddMember(roles: new[] { role });
        Assert.Equal(new[] { "Old name" }, (await _env.Access.GetAccessAsync(holder, _env.Scope)).Roles);

        await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { ConcurrencyStamp = role.ConcurrencyStamp, Name = "New name" });

        Assert.Equal(new[] { "New name" }, (await _env.Access.GetAccessAsync(holder, _env.Scope)).Roles);
        Assert.NotEqual(Guid.Empty, owner);
    }

    [Fact]
    public async Task SetPermissions_AddsAndRemoves_AndAuditsBoth()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create", "products.view" });

        var dto = ResultAssert.Ok(await Svc.SetPermissionsAsync(
            role.Id, new SetAccessRolePermissionsRequest { ConcurrencyStamp = role.ConcurrencyStamp, Permissions = { "products.view", "sales.refund" } }));

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

        var dto = ResultAssert.Ok(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest { ConcurrencyStamp = role.ConcurrencyStamp }));

        Assert.Empty(dto.Permissions);
        Assert.Empty(_env.Read(db => db.RolePermissions.ToList()));
    }

    [Fact]
    public async Task SetPermissions_NothingChanged_IsASilentSuccess()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Ok(await Svc.SetPermissionsAsync(
            role.Id, new SetAccessRolePermissionsRequest { ConcurrencyStamp = role.ConcurrencyStamp, Permissions = { "sales.create" } }));

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }

    [Fact]
    public async Task SetPermissions_RejectsUnknownPermissions_AndUnknownRoles()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(
            await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest { ConcurrencyStamp = role.ConcurrencyStamp, Permissions = { "made.up" } }),
            AccessErrorKind.Validation);
        ResultAssert.Fails(
            await Svc.SetPermissionsAsync(Guid.NewGuid(), new SetAccessRolePermissionsRequest { ConcurrencyStamp = Guid.NewGuid() }), AccessErrorKind.NotFound);
    }

    [Fact]
    public async Task SetPermissions_CannotAddAPermissionTheCallerLacks()
    {
        _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Fails(
            await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest { ConcurrencyStamp = role.ConcurrencyStamp, Permissions = { "sales.create", "sales.refund" } }),
            AccessErrorKind.Forbidden, "access.escalation");
    }

    [Fact]
    public async Task SetPermissions_CannotStripARoleThatIsStrongerThanTheCaller()
    {
        _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");
        var role = _env.AddRole("Boss", new[] { "sales.create", "sales.refund" });

        ResultAssert.Fails(
            await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest { ConcurrencyStamp = role.ConcurrencyStamp, Permissions = { "sales.create" } }),
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

        await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest { ConcurrencyStamp = role.ConcurrencyStamp, Permissions = { "products.view" } });

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

    [Fact]
    public async Task Update_DefaultConcurrencyStamp_IsRequired()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest { Name = "Teller" }),
            AccessErrorKind.Validation, "access.concurrency_stamp_required");
    }

    [Fact]
    public async Task Update_EmptyConcurrencyStamp_IsRequired()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest
        {
            ConcurrencyStamp = Guid.Empty,
            Name = "Teller"
        }), AccessErrorKind.Validation, "access.concurrency_stamp_required");
    }

    [Fact]
    public async Task Update_StaleConcurrencyStamp_ReturnsSaveConflict()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest
        {
            ConcurrencyStamp = Guid.NewGuid(),
            Name = "Teller"
        }), AccessErrorKind.Conflict, "access.save_conflict");
    }

    [Fact]
    public async Task Update_CurrentConcurrencyStamp_PermitsOperation_AndRotatesStamp()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        var dto = ResultAssert.Ok(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest
        {
            ConcurrencyStamp = role.ConcurrencyStamp,
            Name = "Teller"
        }));

        Assert.Equal("Teller", dto.Name);
        Assert.NotEqual(role.ConcurrencyStamp, dto.ConcurrencyStamp);
    }

    [Fact]
    public async Task Update_SuccessfulMutation_RotatesConcurrencyStamp()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        var dto1 = ResultAssert.Ok(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest
        {
            ConcurrencyStamp = role.ConcurrencyStamp,
            Name = "Teller"
        }));

        var dto2 = ResultAssert.Ok(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest
        {
            ConcurrencyStamp = dto1.ConcurrencyStamp,
            Name = "Clerk"
        }));

        Assert.NotEqual(dto1.ConcurrencyStamp, dto2.ConcurrencyStamp);
    }

    [Fact]
    public async Task Update_NoopWithCorrectStamp_PreservesNoopBehavior()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Ok(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest
        {
            ConcurrencyStamp = role.ConcurrencyStamp,
            Name = "Cashier"
        }));

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
        Assert.Equal(role.ConcurrencyStamp, _env.Read(db => db.Roles.Single(r => r.Id == role.Id)).ConcurrencyStamp);
    }

    [Fact]
    public async Task Update_StaleStamp_LeavesRoleAndAuditUnchanged()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest
        {
            ConcurrencyStamp = Guid.NewGuid(),
            Name = "Teller"
        }), AccessErrorKind.Conflict, "access.save_conflict");

        Assert.Equal("Cashier", _env.Read(db => db.Roles.Single(r => r.Id == role.Id)).Name);
        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }

    [Fact]
    public async Task Update_AuthorizationCheckedBeforeConcurrency()
    {
        _env.SignInAs(AccessPermissionNames.RolesRead);
        var role = _env.AddRole("Cashier");

        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest
        {
            ConcurrencyStamp = Guid.Empty,
            Name = "Teller"
        }), AccessErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task Update_ConcurrencyCheckedBeforeEscalation()
    {
        _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");
        var role = _env.AddRole("Boss", new[] { "sales.refund" });

        ResultAssert.Fails(await Svc.UpdateAsync(role.Id, new UpdateAccessRoleRequest
        {
            ConcurrencyStamp = Guid.NewGuid(),
            Name = "Teller"
        }), AccessErrorKind.Conflict, "access.save_conflict");
    }

    [Fact]
    public async Task SetPermissions_DefaultConcurrencyStamp_IsRequired()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Fails(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest
        {
            Permissions = { "sales.refund" }
        }), AccessErrorKind.Validation, "access.concurrency_stamp_required");
    }

    [Fact]
    public async Task SetPermissions_EmptyConcurrencyStamp_IsRequired()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Fails(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest
        {
            ConcurrencyStamp = Guid.Empty,
            Permissions = { "sales.refund" }
        }), AccessErrorKind.Validation, "access.concurrency_stamp_required");
    }

    [Fact]
    public async Task SetPermissions_StaleConcurrencyStamp_ReturnsSaveConflict()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Fails(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest
        {
            ConcurrencyStamp = Guid.NewGuid(),
            Permissions = { "sales.refund" }
        }), AccessErrorKind.Conflict, "access.save_conflict");
    }

    [Fact]
    public async Task SetPermissions_CurrentConcurrencyStamp_PermitsOperation_AndRotatesStamp()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        var dto = ResultAssert.Ok(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest
        {
            ConcurrencyStamp = role.ConcurrencyStamp,
            Permissions = { "sales.refund" }
        }));

        Assert.Contains("sales.refund", dto.Permissions);
        Assert.NotEqual(role.ConcurrencyStamp, dto.ConcurrencyStamp);
    }

    [Fact]
    public async Task SetPermissions_SuccessfulMutation_RotatesConcurrencyStamp()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        var dto1 = ResultAssert.Ok(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest
        {
            ConcurrencyStamp = role.ConcurrencyStamp,
            Permissions = { "sales.create", "sales.refund" }
        }));

        var dto2 = ResultAssert.Ok(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest
        {
            ConcurrencyStamp = dto1.ConcurrencyStamp,
            Permissions = { "sales.create" }
        }));

        Assert.NotEqual(dto1.ConcurrencyStamp, dto2.ConcurrencyStamp);
    }

    [Fact]
    public async Task SetPermissions_NoopWithCorrectStamp_PreservesNoopBehavior()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Ok(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest
        {
            ConcurrencyStamp = role.ConcurrencyStamp,
            Permissions = { "sales.create" }
        }));

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
        Assert.Equal(role.ConcurrencyStamp, _env.Read(db => db.Roles.Single(r => r.Id == role.Id)).ConcurrencyStamp);
    }

    [Fact]
    public async Task SetPermissions_StaleStamp_LeavesRoleAndAuditUnchanged()
    {
        _env.SignInOwner();
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Fails(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest
        {
            ConcurrencyStamp = Guid.NewGuid(),
            Permissions = { "sales.refund" }
        }), AccessErrorKind.Conflict, "access.save_conflict");

        Assert.Single(_env.Read(db => db.RolePermissions.Where(p => p.RoleId == role.Id).ToList()));
        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }

    [Fact]
    public async Task SetPermissions_AuthorizationCheckedBeforeConcurrency()
    {
        _env.SignInAs(AccessPermissionNames.RolesRead);
        var role = _env.AddRole("Cashier", new[] { "sales.create" });

        ResultAssert.Fails(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest
        {
            ConcurrencyStamp = Guid.Empty,
            Permissions = { "sales.refund" }
        }), AccessErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task SetPermissions_ConcurrencyCheckedBeforeEscalation()
    {
        _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");
        var role = _env.AddRole("Boss", new[] { "sales.create", "sales.refund" });

        ResultAssert.Fails(await Svc.SetPermissionsAsync(role.Id, new SetAccessRolePermissionsRequest
        {
            ConcurrencyStamp = Guid.NewGuid(),
            Permissions = { "sales.create" }
        }), AccessErrorKind.Conflict, "access.save_conflict");
    }

    [Fact]
    public async Task Update_DatabaseConcurrencyRace_ReturnsSaveConflict()
    {
        // Context A is the service's request context: it reads the role with the stamp below.
        // A competing writer (context B) commits a name change and a fresh stamp while the service
        // is between its read and its save. The injected unit of work performs that competing
        // commit deterministically, immediately before the real save runs.
        var competitor = new CompetingWriterUnitOfWork();
        using var env = new TestEnv(unitOfWork: competitor);
        env.SignInOwner();
        var role = env.AddRole("Cashier", new[] { "sales.create" });

        // Context A keeps the original stamp and sends it with the request.
        var originalStamp = env.Read(db => db.Roles.Single(r => r.Id == role.Id)).ConcurrencyStamp;

        competitor.Configure(
            env.NewContext,
            new AccessUnitOfWork(env.Db, NullLogger<AccessUnitOfWork>.Instance),
            role.Id,
            "Updated by B");

        var result = await env.CreateRoleService().UpdateAsync(role.Id, new UpdateAccessRoleRequest
        {
            ConcurrencyStamp = originalStamp,
            Name = "Updated by A"
        });

        // The losing request is answered from the EF Core concurrency exception path.
        ResultAssert.Fails(result, AccessErrorKind.Conflict, "access.save_conflict");
        Assert.True(competitor.Committed);

        // Context A's change and the audit entry it staged are rolled back; context B's update stands.
        var roleInDb = env.Read(db => db.Roles.Single(r => r.Id == role.Id));
        Assert.Equal("Updated by B", roleInDb.Name);
        Assert.Equal(competitor.CompetingStamp, roleInDb.ConcurrencyStamp);
        Assert.NotEqual(originalStamp, roleInDb.ConcurrencyStamp);

        var auditActions = env.Read(db => db.AuditEntries.Select(a => a.Action).ToList());
        Assert.DoesNotContain(AccessAuditActions.RoleUpdated, auditActions);
    }
}

/// <summary>
/// Deterministic seam for the service-level database race: on the first save it commits a competing
/// role change with a rotated concurrency stamp from a separate context, then delegates to the real
/// unit of work. Because the competing stamp no longer matches context A's original stamp, the
/// service's own save loses the optimistic concurrency check and must return save_conflict.
/// </summary>
internal sealed class CompetingWriterUnitOfWork : IAccessUnitOfWork
{
    private Func<AccessDbContext>? _newContext;
    private IAccessUnitOfWork? _inner;
    private Guid _roleId;
    private string _competingName = "Updated by B";

    public bool Committed { get; private set; }

    public Guid CompetingStamp { get; private set; }

    public void Configure(Func<AccessDbContext> newContext, IAccessUnitOfWork inner, Guid roleId, string competingName)
    {
        _newContext = newContext;
        _inner = inner;
        _roleId = roleId;
        _competingName = competingName;
    }

    public async Task<SaveOutcome> SaveAsync(CancellationToken ct = default)
    {
        if (!Committed)
        {
            CompetingStamp = Guid.NewGuid();

            using var competing = _newContext!();
            var role = await competing.Roles.SingleAsync(r => r.Id == _roleId, ct);

            // Context B must rotate the stamp; otherwise the database would still hold context A's
            // original value and the losing save would succeed instead of conflicting.
            role.Name = _competingName;
            role.ConcurrencyStamp = CompetingStamp;

            await competing.SaveChangesAsync(ct);
            Committed = true;
        }

        return await _inner!.SaveAsync(ct);
    }
}