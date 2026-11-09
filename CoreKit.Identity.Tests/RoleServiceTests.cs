namespace CoreKit.Identity.Tests;

/// <summary>
/// What this protects: creating, changing and removing roles, and assigning permissions to them --
/// including the Administrator role protections that keep the system from being crippled.
/// Why it matters: if an ordinary actor could rename or delete the Administrator role, or strip
/// it of its permissions, the role-based access control system would be broken.
/// The rule under test (see RoleService): the Administrator role cannot be renamed, deleted or
/// limited; it always holds every permission; role names must be unique within the system.
/// </summary>
public sealed class RoleServiceTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    // ---- Create ----

    [Fact]
    public async Task CreateRole_WithValidData_Succeeds()
    {
        var result = await _env.RoleService.CreateAsync(new CreateRoleRequest
        {
            Name = "Auditor",
            Description = "Read-only access",
            PermissionIds = new List<Guid> { _env.UserPermissionId }
        });

        var dto = ResultAssert.Ok(result);
        Assert.Equal("Auditor", dto.Name);
        Assert.Equal("Read-only access", dto.Description);
        Assert.True(dto.IsActive);
        Assert.Contains(IdentityPermissionNames.UsersRead, dto.Permissions);
    }

    [Fact]
    public async Task CreateRole_WithDuplicateName_Fails()
    {
        var result = await _env.RoleService.CreateAsync(new CreateRoleRequest
        {
            Name = "ADMINISTRATOR",
            Description = "Dup"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Conflict, "conflict");
    }

    [Fact]
    public async Task CreateRole_WithEmptyName_Fails()
    {
        var result = await _env.RoleService.CreateAsync(new CreateRoleRequest
        {
            Name = ""
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    [Fact]
    public async Task CreateRole_WithNonExistentPermission_Fails()
    {
        var result = await _env.RoleService.CreateAsync(new CreateRoleRequest
        {
            Name = "BadPerm",
            PermissionIds = new List<Guid> { Guid.NewGuid() }
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    // ---- Get ----

    [Fact]
    public async Task GetRole_ThatExists_ReturnsIt()
    {
        var dto = ResultAssert.Ok(await _env.RoleService.GetAsync(_env.AdministratorRoleId));

        Assert.Equal(IdentityRoleNames.Administrator, dto.Name);
        Assert.True(dto.IsProtected);
    }

    [Fact]
    public async Task GetRole_ThatDoesNotExist_Fails()
    {
        ResultAssert.Fails(await _env.RoleService.GetAsync(Guid.NewGuid()),
            IdentityErrorKind.NotFound, "not_found");
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateRole_RenameNonAdministrator_Succeeds()
    {
        // Manager is not protected, so it can be renamed.
        var result = await _env.RoleService.UpdateAsync(_env.ManagerRoleId, new UpdateRoleRequest
        {
            Name = "Supervisor"
        });

        Assert.Equal("Supervisor", ResultAssert.Ok(result).Name);
    }

    [Fact]
    public async Task UpdateRole_RenameAdministrator_Fails()
    {
        ResultAssert.Fails(await _env.RoleService.UpdateAsync(_env.AdministratorRoleId, new UpdateRoleRequest
        {
            Name = "God"
        }), IdentityErrorKind.Forbidden, "forbidden");
    }

    [Fact]
    public async Task UpdateRole_AdministratorAlwaysHasEveryPermission()
    {
        // Administrator already has all permissions seeded. Trying to change its permissions
        // should be refused because it's protected.
        ResultAssert.Fails(await _env.RoleService.SetPermissionsAsync(_env.AdministratorRoleId,
            new SetRolePermissionsRequest()), IdentityErrorKind.Forbidden, "forbidden");
    }

    [Fact]
    public async Task UpdateRole_ChangePermissionsOfNonAdministrator_Succeeds()
    {
        var result = await _env.RoleService.SetPermissionsAsync(_env.StaffRoleId,
            new SetRolePermissionsRequest
            {
                PermissionIds = new List<Guid> { _env.UserCreatePermissionId }
            });

        var dto = ResultAssert.Ok(result);
        Assert.Contains(IdentityPermissionNames.UsersCreate, dto.Permissions);
        Assert.DoesNotContain(IdentityPermissionNames.UsersRead, dto.Permissions);
    }

    // ---- Delete ----

    [Fact]
    public async Task DeleteRole_ThatIsNotAdministrator_Succeeds()
    {
        // Create a temporary role to delete.
        var createResult = await _env.RoleService.CreateAsync(new CreateRoleRequest
        {
            Name = "ToDelete",
            PermissionIds = new List<Guid> { _env.UserPermissionId }
        });
        var tempRoleId = ResultAssert.Ok(createResult).Id;

        ResultAssert.Ok(await _env.RoleService.DeleteAsync(tempRoleId));

        ResultAssert.Fails(await _env.RoleService.GetAsync(tempRoleId),
            IdentityErrorKind.NotFound, "not_found");
    }

    [Fact]
    public async Task DeleteAdministratorRole_Fails()
    {
        ResultAssert.Fails(await _env.RoleService.DeleteAsync(_env.AdministratorRoleId),
            IdentityErrorKind.Forbidden, "forbidden");
    }

    [Fact]
    public async Task DeleteRoleAssignedToUser_Fails()
    {
        // Staff role is assigned to StaffUserId.
        ResultAssert.Fails(await _env.RoleService.DeleteAsync(_env.StaffRoleId),
            IdentityErrorKind.Conflict, "conflict");
    }

    // ---- Authorization ----

    [Fact]
    public async Task CreateRole_WithoutRolesCreatePermission_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.RoleService.CreateAsync(new CreateRoleRequest
        {
            Name = "Auditor",
            PermissionIds = new List<Guid> { _env.UserPermissionId }
        }), IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task GetRole_WithoutRolesReadPermission_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.RoleService.GetAsync(_env.AdministratorRoleId),
            IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task UpdateRole_WithoutRolesUpdatePermission_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.RoleService.UpdateAsync(_env.ManagerRoleId, new UpdateRoleRequest
        {
            Name = "Supervisor"
        }), IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task DeleteRole_WithoutRolesDeletePermission_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.RoleService.DeleteAsync(_env.StaffRoleId),
            IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task SetPermissions_WithoutRolesUpdatePermission_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.RoleService.SetPermissionsAsync(_env.StaffRoleId,
            new SetRolePermissionsRequest
            {
                PermissionIds = new List<Guid> { _env.UserCreatePermissionId }
            }), IdentityErrorKind.Forbidden, "access.denied");
    }
}
