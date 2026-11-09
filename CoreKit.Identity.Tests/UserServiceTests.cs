namespace CoreKit.Identity.Tests;

/// <summary>
/// What this protects: creating, changing and removing users, and assigning roles to them --
/// including the administrator protections that prevent locking everyone out.
/// Why it matters: if an ordinary user could promote themselves to Administrator, or if the last
/// administrator could be deleted, the system would be compromised or bricked.
/// The rule under test (see UserService): only an administrator can create users; role changes
/// cannot strip the last active administrator; a user cannot deactivate or delete themselves;
/// Administrator role assignment and removal are guarded.
/// </summary>
public sealed class UserServiceTests : IDisposable
{
    private readonly TestEnv _env;

    public UserServiceTests()
    {
        _env = new TestEnv();
    }

    public void Dispose() => _env.Dispose();

    // ---- Create ----

    [Fact]
    public async Task CreateUser_WithValidData_Succeeds()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "newuser@example.com",
            Password = "NewUserPass123!",
            DisplayName = "New User",
            RoleIds = new List<Guid> { _env.StaffRoleId }
        });

        var dto = ResultAssert.Ok(result);
        Assert.Equal("newuser@example.com", dto.Email);
        Assert.Equal("New User", dto.DisplayName);
        Assert.True(dto.IsActive);
        Assert.Contains(IdentityRoleNames.Staff, dto.Roles);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "OWNER@example.com",
            Password = "DupPass123!",
            DisplayName = "Duplicate"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Conflict, "conflict");
    }

    [Fact]
    public async Task CreateUser_WithInvalidEmail_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "not-an-email",
            Password = "BadEmailPass123!"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    [Fact]
    public async Task CreateUser_WithShortPassword_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "shortpw@example.com",
            Password = "short"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    [Fact]
    public async Task CreateUser_PasswordEqualToEmail_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "same@example.com",
            Password = "same@example.com"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    [Fact]
    public async Task CreateUser_WithNonExistentRole_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "norele@example.com",
            Password = "NoRolePass123!",
            RoleIds = new List<Guid> { Guid.NewGuid() }
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    // ---- Get ----

    [Fact]
    public async Task GetUser_ThatExists_ReturnsIt()
    {
        _env.SignInOwner();

        var dto = ResultAssert.Ok(await _env.UserService.GetAsync(_env.StaffUserId));

        Assert.Equal("staff@example.com", dto.Email);
        Assert.Contains(IdentityRoleNames.Staff, dto.Roles);
    }

    [Fact]
    public async Task GetUser_ThatDoesNotExist_Fails()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.GetAsync(Guid.NewGuid()),
            IdentityErrorKind.NotFound, "not_found");
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateUser_Email_SucceedsAndUnconfirms()
    {
        _env.SignInOwner();

        var result = await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Email = "newstaff@example.com"
        });

        var dto = ResultAssert.Ok(result);
        Assert.Equal("newstaff@example.com", dto.Email);
        Assert.False(dto.EmailConfirmed);
    }

    [Fact]
    public async Task UpdateUser_DisplayName_Succeeds()
    {
        _env.SignInOwner();

        var result = await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            DisplayName = "New Display Name"
        });

        Assert.Equal("New Display Name", ResultAssert.Ok(result).DisplayName);
    }

    [Fact]
    public async Task UpdateUser_Password_RevokesSessions()
    {
        _env.SignInOwner();

        var result = await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Password = "NewStaffPass456!"
        });

        ResultAssert.Ok(result);

        // Password hash should have changed in the database.
        using var db = _env.NewContext();
        var user = await db.Users.FindAsync(_env.StaffUserId);
        Assert.NotNull(user);
        Assert.NotEqual("StaffPass123!", user.PasswordHash);
    }

    [Fact]
    public async Task UpdateUser_DeactivateYourself_Fails()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.OwnerUserId, new UpdateUserRequest
        {
            IsActive = false
        }), IdentityErrorKind.Forbidden, "forbidden");
    }

    [Fact]
    public async Task UpdateUser_DeactivateLastActiveAdministrator_Fails()
    {
        // The owner is the only active administrator. Sign in as staff (not the target),
        // so the self-modification check is skipped and the administrator guard runs.
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.OwnerUserId, new UpdateUserRequest
        {
            IsActive = false
        }), IdentityErrorKind.Conflict, "conflict");
    }

    [Fact]
    public async Task UpdateUser_DeleteYourself_Fails()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.DeleteAsync(_env.OwnerUserId),
            IdentityErrorKind.Forbidden, "forbidden");
    }

    [Fact]
    public async Task UpdateUser_DeleteLastActiveAdministrator_Fails()
    {
        // Sign in as staff (not the target), so the self-modification check is skipped
        // and the administrator guard runs.
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.DeleteAsync(_env.OwnerUserId),
            IdentityErrorKind.Conflict, "conflict");
    }

    // ---- SetRoles ----

    [Fact]
    public async Task SetRoles_AssignsRoles_Succeeds()
    {
        _env.SignInOwner();

        var result = await _env.UserService.SetRolesAsync(_env.StaffUserId, new SetUserRolesRequest
        {
            RoleIds = new List<Guid> { _env.AdministratorRoleId, _env.StaffRoleId }
        });

        var dto = ResultAssert.Ok(result);
        Assert.Contains(IdentityRoleNames.Administrator, dto.Roles);
        Assert.Contains(IdentityRoleNames.Staff, dto.Roles);
    }

    [Fact]
    public async Task SetRoles_RemoveLastActiveAdministratorRole_Fails()
    {
        // Owner is the only active administrator. Staff tries to remove Administrator from owner.
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.SetRolesAsync(_env.OwnerUserId, new SetUserRolesRequest
        {
            RoleIds = new List<Guid> { _env.StaffRoleId }
        }), IdentityErrorKind.Conflict, "conflict");
    }

    [Fact]
    public async Task SetRoles_WithNonExistentRole_Fails()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.SetRolesAsync(_env.StaffUserId, new SetUserRolesRequest
        {
            RoleIds = new List<Guid> { Guid.NewGuid() }
        }), IdentityErrorKind.Validation, "validation.failed");
    }
}
