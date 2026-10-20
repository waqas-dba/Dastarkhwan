using CoreKit.Architecture.Tests.Support;

namespace CoreKit.Access.Tests;

public sealed class AccessAssignmentServiceTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private AccessAssignmentService Svc => _env.CreateAssignmentService();

    private static SetAccessUserRolesRequest Roles(params AccessRole[] roles)
        => new() { RoleIds = roles.Select(r => r.Id).ToList() };

    [Fact]
    public async Task SetRoles_GivesAMemberARole_AndRecordsWhoDidIt()
    {
        var owner = _env.SignInOwner();
        var cashier = _env.AddRole("Cashier", new[] { "sales.create" });
        var staff = _env.AddMember();

        var roles = ResultAssert.Ok(await Svc.SetRolesAsync(staff, Roles(cashier)));

        Assert.Equal("Cashier", Assert.Single(roles).Name);

        var assignment = _env.Read(db => db.Assignments.Single());
        Assert.Equal(staff, assignment.UserId);
        Assert.Equal(cashier.Id, assignment.RoleId);
        Assert.Equal(owner, assignment.AssignedBy);
        Assert.Equal(_env.Scope.Id, assignment.ScopeId);
        Assert.Equal(_env.UtcNow, assignment.AssignedAt);

        var audit = _env.Read(db => db.AuditEntries.Single(a => a.Action == AccessAuditActions.RolesAssigned));
        Assert.Contains("Cashier", audit.Details);
    }

    [Fact]
    public async Task SetRoles_ReplacesTheWholeSet()
    {
        _env.SignInOwner();
        var cashier = _env.AddRole("Cashier", new[] { "sales.create" });
        var stock = _env.AddRole("Stock", new[] { "products.manage" });
        var staff = _env.AddMember(roles: new[] { cashier });

        var roles = ResultAssert.Ok(await Svc.SetRolesAsync(staff, Roles(stock)));

        Assert.Equal(new[] { "Stock" }, roles.Select(r => r.Name));
        Assert.Equal(stock.Id, _env.Read(db => db.Assignments.Single()).RoleId);
    }

    [Fact]
    public async Task SetRoles_WithNoRoles_RemovesThemAll()
    {
        _env.SignInOwner();
        var cashier = _env.AddRole("Cashier");
        var staff = _env.AddMember(roles: new[] { cashier });

        var roles = ResultAssert.Ok(await Svc.SetRolesAsync(staff, new SetAccessUserRolesRequest()));

        Assert.Empty(roles);
        Assert.Empty(_env.Read(db => db.Assignments.ToList()));
    }

    [Fact]
    public async Task SetRoles_TheSameSet_IsASilentSuccess()
    {
        _env.SignInOwner();
        var cashier = _env.AddRole("Cashier");
        var staff = _env.AddMember(roles: new[] { cashier });

        ResultAssert.Ok(await Svc.SetRolesAsync(staff, Roles(cashier)));

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }

    [Fact]
    public async Task SetRoles_IgnoresRepeatedAndEmptyRoleIds()
    {
        _env.SignInOwner();
        var cashier = _env.AddRole("Cashier");
        var staff = _env.AddMember();

        var request = new SetAccessUserRolesRequest { RoleIds = { cashier.Id, cashier.Id, Guid.Empty } };

        Assert.Single(ResultAssert.Ok(await Svc.SetRolesAsync(staff, request)));
        Assert.Single(_env.Read(db => db.Assignments.ToList()));
    }

    [Fact]
    public async Task SetRoles_OnlyForPeopleWhoBelongToTheScope()
    {
        _env.SignInOwner();
        var cashier = _env.AddRole("Cashier");

        ResultAssert.Fails(await Svc.SetRolesAsync(Guid.NewGuid(), Roles(cashier)), AccessErrorKind.Validation);
        ResultAssert.Fails(await Svc.SetRolesAsync(Guid.Empty, Roles(cashier)), AccessErrorKind.Validation);
        Assert.Empty(_env.Read(db => db.Assignments.ToList()));
    }

    [Fact]
    public async Task SetRoles_UnknownRolesAndRolesOfAnotherScopeAreNotFound()
    {
        _env.SignInOwner();
        var staff = _env.AddMember();
        var foreign = _env.AddRole("Foreign", scope: AccessScope.ForTenant(Guid.NewGuid()));

        ResultAssert.Fails(
            await Svc.SetRolesAsync(staff, new SetAccessUserRolesRequest { RoleIds = { Guid.NewGuid() } }), AccessErrorKind.NotFound);
        ResultAssert.Fails(await Svc.SetRolesAsync(staff, Roles(foreign)), AccessErrorKind.NotFound);
    }

    [Fact]
    public async Task SetRoles_EnforcesTheLimitPerPerson()
    {
        using var env = new TestEnv(new AccessOptions { MaxRolesPerUser = 1 });
        env.SignInOwner();
        var staff = env.AddMember();

        ResultAssert.Fails(
            await env.CreateAssignmentService().SetRolesAsync(staff, new SetAccessUserRolesRequest
            {
                RoleIds = { env.AddRole("One").Id, env.AddRole("Two").Id }
            }),
            AccessErrorKind.Conflict);
    }

    [Fact]
    public async Task SetRoles_NeedsTheManagePermission()
    {
        _env.SignInAs(AccessPermissionNames.AssignmentsRead);
        var cashier = _env.AddRole("Cashier");
        var staff = _env.AddMember();

        ResultAssert.Fails(await Svc.SetRolesAsync(staff, Roles(cashier)), AccessErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task SetRoles_CannotGiveARoleStrongerThanTheCaller()
    {
        _env.SignInAs(AccessPermissionNames.AssignmentsManage, "sales.create");
        var seller = _env.AddRole("Seller", new[] { "sales.create" });
        var boss = _env.AddRole("Boss", new[] { "sales.create", "sales.refund" });
        var staff = _env.AddMember();

        ResultAssert.Fails(await Svc.SetRolesAsync(staff, Roles(boss)), AccessErrorKind.Forbidden, "access.escalation");
        ResultAssert.Ok(await Svc.SetRolesAsync(staff, Roles(seller)));
    }

    [Fact]
    public async Task SetRoles_CannotTakeAwayARoleStrongerThanTheCaller()
    {
        _env.SignInAs(AccessPermissionNames.AssignmentsManage, "sales.create");
        var boss = _env.AddRole("Boss", new[] { "sales.create", "sales.refund" });
        var staff = _env.AddMember(roles: new[] { boss });

        ResultAssert.Fails(await Svc.SetRolesAsync(staff, new SetAccessUserRolesRequest()), AccessErrorKind.Forbidden, "access.escalation");
        Assert.Single(_env.Read(db => db.Assignments.ToList()));
    }

    [Fact]
    public async Task SetRoles_NobodyCanPromoteThemselvesPastTheirOwnLevel()
    {
        var caller = _env.SignInAs(AccessPermissionNames.AssignmentsManage);
        var boss = _env.AddRole("Boss", new[] { "sales.refund" });

        ResultAssert.Fails(await Svc.SetRolesAsync(caller, Roles(boss)), AccessErrorKind.Forbidden, "access.escalation");
    }

    [Fact]
    public async Task SetRoles_TakesEffectAtOnce()
    {
        _env.SignInOwner();
        var cashier = _env.AddRole("Cashier", new[] { "sales.create" });
        var staff = _env.AddMember();
        Assert.False((await _env.Access.GetAccessAsync(staff, _env.Scope)).Has("sales.create"));

        await Svc.SetRolesAsync(staff, Roles(cashier));

        Assert.True((await _env.Access.GetAccessAsync(staff, _env.Scope)).Has("sales.create"));

        await Svc.SetRolesAsync(staff, new SetAccessUserRolesRequest());

        Assert.False((await _env.Access.GetAccessAsync(staff, _env.Scope)).Has("sales.create"));
    }

    [Fact]
    public async Task GetRoles_ShowsWhatAPersonHolds_AndNeedsTheReadPermission()
    {
        var cashier = _env.AddRole("Cashier", new[] { "sales.create" });
        var staff = _env.AddMember(roles: new[] { cashier });

        _env.SignInAs("sales.create");
        ResultAssert.Fails(await Svc.GetRolesAsync(staff), AccessErrorKind.Forbidden, "access.denied");

        _env.SignInAs(AccessPermissionNames.AssignmentsRead);
        var roles = ResultAssert.Ok(await Svc.GetRolesAsync(staff));

        Assert.Equal("Cashier", Assert.Single(roles).Name);
        Assert.Equal(1, roles[0].MemberCount);
        ResultAssert.Fails(await Svc.GetRolesAsync(Guid.Empty), AccessErrorKind.Validation);
    }
}