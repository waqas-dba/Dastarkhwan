namespace CoreKit.Access.Tests;

public sealed class PlatformMembershipTests : IDisposable
{
    private readonly TestEnv _env;

    public PlatformMembershipTests()
    {
        _env = new(catalog: b =>
        {
            // Tenant permissions and templates
            b.Add("sales.create", "Ring up a sale")
             .Add("sales.refund", "Refund a sale")
             .Add("products.view", "See products")
             .Add("products.manage", "Create and edit products");

            b.AddRoleTemplate("Manager", "Runs the shop",
                "sales.create", "sales.refund", "products.view", "products.manage",
                AccessPermissionNames.RolesRead, AccessPermissionNames.AssignmentsRead, AccessPermissionNames.AssignmentsManage);
            b.AddRoleTemplate("Cashier", "Rings up sales", "sales.create", "products.view");

            // Platform permissions and templates
            b.Add("platform.members.manage", "Manage platform members");
            b.Add("platform.roles.manage", "Manage platform roles");

            b.AddRoleTemplateForScope("PlatformAdmin", "Platform administrator",
                AccessScope.PlatformType,
                "platform.members.manage", "platform.roles.manage");
        });

        PlatformScope = AccessScope.ForPlatform(AccessScope.PlatformId);
    }

    public void Dispose() => _env.Dispose();

    private AccessScope PlatformScope { get; }
    private AccessScope TenantScope => _env.Scope;
    private PlatformMembershipService Svc =>
        new(_env.Db, _env.UnitOfWork, _env.Audit, _env.User, _env.Cache, _env.Time);

    // ---- platform membership is independent of assignments ----

    [Fact]
    public async Task PlatformMemberWithoutAssignmentsGrantsNothing()
    {
        // Arrange: platform member with no roles
        var user = Guid.NewGuid();
        _env.Membership.Set(PlatformScope, user, isOwner: false, membershipGeneration: 1);

        // Act
        var access = await _env.Access.GetAccessAsync(user, PlatformScope);

        // Assert: member but no permissions
        Assert.True(access.IsMember);
        Assert.False(access.IsOwner);
        Assert.Empty(access.Permissions);
        Assert.Empty(access.Roles);
    }

    [Fact]
    public async Task PlatformMemberWithAssignmentGetsPermissions()
    {
        // Arrange
        _env.SignInOwner();
        var adminRole = _env.AddRole("PlatformAdmin", new[] { "platform.members.manage" }, PlatformScope);
        var user = Guid.NewGuid();
        _env.Membership.Set(PlatformScope, user, membershipGeneration: 1);
        _env.Assign(user, adminRole);

        // Act
        var access = await _env.Access.GetAccessAsync(user, PlatformScope);

        // Assert
        Assert.True(access.IsMember);
        Assert.True(access.Has("platform.members.manage"));
        Assert.Contains("PlatformAdmin", access.Roles);
    }

    // ---- platform and tenant membership are isolated ----

    [Fact]
    public async Task TenantMemberGetsNoPlatformAccess()
    {
        // Arrange: user is a tenant member but not a platform member
        var user = Guid.NewGuid();
        _env.Membership.Set(TenantScope, user, membershipGeneration: 1);

        // Act
        var access = await _env.Access.GetAccessAsync(user, PlatformScope);

        // Assert: not a platform member
        Assert.False(access.IsMember);
        Assert.Empty(access.Permissions);
    }

    [Fact]
    public async Task PlatformMemberGetsNoTenantAccess()
    {
        // Arrange: user is a platform member but not a tenant member
        var user = Guid.NewGuid();
        _env.Membership.Set(PlatformScope, user, membershipGeneration: 1);

        // Act
        var access = await _env.Access.GetAccessAsync(user, TenantScope);

        // Assert: not a tenant member
        Assert.False(access.IsMember);
        Assert.Empty(access.Permissions);
    }

    // ---- membership generations prevent historical assignments ----

    [Fact]
    public async Task AssignmentFromEarlierGenerationGrantsNothingAfterRevoke()
    {
        // Arrange: member gets a role at generation 1, then is revoked (generation 2)
        _env.SignInOwner();
        var role = _env.AddRole("TestRole", new[] { "platform.members.manage" }, PlatformScope);
        var user = Guid.NewGuid();
        _env.Membership.Set(PlatformScope, user, membershipGeneration: 1);
        _env.Assign(user, role);

        // Act: revoke (generation becomes 2)
        var result = await Svc.RevokeMemberAsync(user);
        Assert.True(result.IsSuccess);
        _env.Membership.Remove(PlatformScope, user);

        // Assert: old assignment no longer grants permissions
        var access = await _env.Access.GetAccessAsync(user, PlatformScope);
        Assert.False(access.IsMember);
        Assert.False(access.Has("platform.members.manage"));
    }

    [Fact]
    public async Task RejoiningCreatesNewGenerationAndOldAssignmentsStayInactive()
    {
        // Arrange: member at generation 1 with a role
        _env.SignInOwner();
        var role = _env.AddRole("TestRole", new[] { "platform.members.manage" }, PlatformScope);
        var user = Guid.NewGuid();
        _env.Membership.Set(PlatformScope, user, membershipGeneration: 1);
        _env.Assign(user, role);

        // Act: revoke then rejoin
        await Svc.RevokeMemberAsync(user);
        var rejoin = await Svc.AddMemberAsync(user);
        Assert.True(rejoin.IsSuccess);
        _env.Membership.Set(PlatformScope, user, membershipGeneration: 3);

        // Assert: new generation, but old assignment (generation 1) doesn't grant permissions
        var access = await _env.Access.GetAccessAsync(user, PlatformScope);
        Assert.True(access.IsMember);
        Assert.False(access.Has("platform.members.manage"));
    }

    [Fact]
    public async Task RejoinedMemberWithNewAssignmentGetsPermissions()
    {
        // Arrange: member at generation 1, revoked (gen 2), rejoined (gen 3), new role assigned at gen 3
        _env.SignInOwner();
        var role = _env.AddRole("TestRole", new[] { "platform.members.manage" }, PlatformScope);
        var user = Guid.NewGuid();

        _env.Membership.Set(PlatformScope, user, membershipGeneration: 1);
        _env.Assign(user, role); // gen 1 assignment
        await Svc.RevokeMemberAsync(user); // gen 2
        await Svc.AddMemberAsync(user); // gen 3

        // Act: assign role at gen 3
        _env.Membership.Set(PlatformScope, user, membershipGeneration: 3);
        _env.Seed(db =>
        {
            var old = db.Assignments.FirstOrDefault(a => a.UserId == user && a.RoleId == role.Id);
            if (old is not null) db.Assignments.Remove(old);
        });
        _env.Assign(user, role, membershipGeneration: 3);

        // Assert: new assignment at gen 3 grants permissions
        var access = await _env.Access.GetAccessAsync(user, PlatformScope);
        Assert.True(access.Has("platform.members.manage"));
    }

    // ---- template scoping ----

    [Fact]
    public async Task ProvisionTenantScopeOnlyProvisionsTenantTemplates()
    {
        // Arrange
        var otherTenant = AccessScope.ForTenant(Guid.NewGuid());
        var svc = _env.CreateMaintenance();

        // Act
        await svc.ProvisionScopeAsync(TenantScope);

        // Assert: tenant templates exist, no platform templates
        var roles = _env.Read(db => db.Roles.Where(r => r.ScopeId == TenantScope.Id).ToList());
        Assert.Equal(2, roles.Count);
        Assert.All(roles, r => Assert.Equal(AccessScope.TenantType, r.ScopeType));
        Assert.DoesNotContain(roles, r => r.Name == "PlatformAdmin");
    }

    [Fact]
    public async Task ProvisionPlatformScopeOnlyProvisionsPlatformTemplates()
    {
        // Arrange
        var svc = _env.CreateMaintenance();

        // Act
        await svc.ProvisionScopeAsync(PlatformScope);

        // Assert: only platform templates exist
        var roles = _env.Read(db => db.Roles.Where(r => r.ScopeId == PlatformScope.Id).ToList());
        Assert.Single(roles);
        Assert.Equal(AccessScope.PlatformType, roles[0].ScopeType);
        Assert.Equal("PlatformAdmin", roles[0].Name);
    }

    [Fact]
    public async Task ProvisioningTwiceDoesNotDuplicateRoles()
    {
        // Arrange
        var svc = _env.CreateMaintenance();

        // Act
        await svc.ProvisionScopeAsync(PlatformScope);
        await svc.ProvisionScopeAsync(PlatformScope);

        // Assert: still only one platform role
        var count = _env.Read(db => db.Roles.Count(r => r.ScopeId == PlatformScope.Id));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddMemberThenSetRolesRecordsGeneration()
    {
        // Arrange
        _env.SignInOwner();
        _env.Membership.Set(PlatformScope, _env.User.UserId ?? Guid.Empty, isOwner: true);
        _env.ScopeContext.Current = PlatformScope;
        var role = _env.AddRole("PlatformAdmin", new[] { "platform.members.manage" }, PlatformScope);
        var user = Guid.NewGuid();

        // Act: add member at generation 1, then assign role
        _env.Membership.Set(PlatformScope, user, membershipGeneration: 1);
        var result = await _env.CreateAssignmentService().SetRolesAsync(user,
            new SetAccessUserRolesRequest { RoleIds = new List<Guid> { role.Id } });

        // Assert: assignment recorded with generation 1
        Assert.True(result.IsSuccess);
        var assignment = _env.Read(db => db.Assignments.Single());
        Assert.Equal(1L, assignment.MembershipGeneration);
        Assert.Equal(user, assignment.UserId);
        Assert.Equal(role.Id, assignment.RoleId);
        Assert.Equal(PlatformScope.Id, assignment.ScopeId);
        Assert.Equal(AccessScope.PlatformType, assignment.ScopeType);
    }

    [Fact]
    public async Task AddMemberIsIdempotentWhenAlreadyActive()
    {
        // Arrange
        var user = Guid.NewGuid();
        _env.Membership.Set(PlatformScope, user, membershipGeneration: 1);

        // Act
        var result1 = await Svc.AddMemberAsync(user);
        var result2 = await Svc.AddMemberAsync(user);

        // Assert: both succeed, no error
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);

        // Assert: still one membership row with generation 1
        var member = _env.Read(db => db.PlatformMembers.Single());
        Assert.Equal(user, member.UserId);
        Assert.Equal(1L, member.MembershipGeneration);
        Assert.Null(member.RevokedAt);
    }

    [Fact]
    public async Task RevokeNonexistentMemberIsNoop()
    {
        // Act
        var result = await Svc.RevokeMemberAsync(Guid.NewGuid());

        // Assert: succeeds (already not a member)
        Assert.True(result.IsSuccess);
        Assert.Empty(_env.Read(db => db.PlatformMembers.ToList()));
    }

    [Fact]
    public async Task PlatformMembershipIsPersistedInAccessDbContext()
    {
        // Arrange
        var user = Guid.NewGuid();

        // Act
        var result = await Svc.AddMemberAsync(user);
        Assert.True(result.IsSuccess);

        // Assert: persisted in ACCESS_PlatformMembers
        var member = _env.Read(db => db.PlatformMembers.Single());
        Assert.Equal(user, member.UserId);
        Assert.Equal(AccessScope.PlatformId, member.PlatformId);
        Assert.Equal(1L, member.MembershipGeneration);
        Assert.Null(member.RevokedAt);
    }
}
