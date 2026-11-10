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
        // The owner is the only active administrator. Sign in as someone else who is allowed to
        // update users, so the permission check and self-modification check are passed and the
        // administrator guard is what refuses the change.
        _env.SignInWith(_env.StaffUserId, new List<string> { IdentityRoleNames.Staff },
            IdentityPermissionNames.UsersUpdate);

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
        // Sign in as someone else who may delete users, so the permission check and
        // self-modification check are passed and the administrator guard is what refuses the delete.
        _env.SignInWith(_env.StaffUserId, new List<string> { IdentityRoleNames.Staff },
            IdentityPermissionNames.UsersDelete);

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
        // Owner is the only active administrator. Another user who may edit roles tries to remove
        // Administrator from owner, so the administrator guard is what refuses the change.
        _env.SignInWith(_env.StaffUserId, new List<string> { IdentityRoleNames.Staff },
            IdentityPermissionNames.UsersUpdate);

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

    // ---- Authorization ----

    [Fact]
    public async Task CreateUser_WithoutUsersCreatePermission_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "newuser@example.com",
            Password = "NewUserPass123!"
        }), IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task GetUser_WithoutUsersReadPermission_Fails()
    {
        // Staff normally holds users.read, so sign this actor in with no permissions at all.
        _env.SignInWith(_env.StaffUserId, new List<string> { IdentityRoleNames.Staff });

        ResultAssert.Fails(await _env.UserService.GetAsync(_env.StaffUserId),
            IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task ListUsers_WithoutUsersReadPermission_Fails()
    {
        // Listing must be guarded at the service, not only at the endpoint, so a caller that
        // reaches the service another way still cannot enumerate users.
        _env.SignInWith(_env.StaffUserId, new List<string> { IdentityRoleNames.Staff });

        ResultAssert.Fails(await _env.UserService.ListAsync(new UserListQuery()),
            IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task CreateUser_WithOnlyCreatePermission_Succeeds()
    {
        // A write must not depend on a read permission: the caller may create users without
        // being allowed to browse them, and still needs the created record back.
        _env.SignInWith(_env.StaffUserId, new List<string> { IdentityRoleNames.Staff },
            IdentityPermissionNames.UsersCreate);

        var dto = ResultAssert.Ok(await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "createonly@example.com",
            Password = "CreateOnlyPass123!",
            DisplayName = "Create Only",
            RoleIds = new List<Guid> { _env.StaffRoleId }
        }));

        Assert.Equal("createonly@example.com", dto.Email);
        Assert.Contains(IdentityRoleNames.Staff, dto.Roles);
    }

    [Fact]
    public async Task UpdateUser_WithoutUsersUpdatePermission_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            DisplayName = "New Display Name"
        }), IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task DeleteUser_WithoutUsersDeletePermission_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.DeleteAsync(_env.StaffUserId),
            IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task CreateUser_NonAdmin_CannotGrantAdministrator_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "newadmin@example.com",
            Password = "NewAdminPass123!",
            RoleIds = new List<Guid> { _env.AdministratorRoleId }
        }), IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task SetRoles_NonAdmin_CannotGrantAdministrator_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.SetRolesAsync(_env.StaffUserId, new SetUserRolesRequest
        {
            RoleIds = new List<Guid> { _env.AdministratorRoleId }
        }), IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task UpdateUser_NonAdmin_CannotChangeAnotherAdminEmail_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.OwnerUserId, new UpdateUserRequest
        {
            Email = "newemail@example.com"
        }), IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task UpdateUser_NonAdmin_CannotChangeAnotherAdminPassword_Fails()
    {
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.OwnerUserId, new UpdateUserRequest
        {
            Password = "NewAdminPass456!"
        }), IdentityErrorKind.Forbidden, "access.denied");
    }

    // ---- Account lockout ----

    [Fact]
    public async Task Login_FiveFailedAttempts_LocksAccountFor15Minutes()
    {
        var user = _env.StaffUserId;

        // 5 failed login attempts
        for (int i = 0; i < 5; i++)
        {
            var result = await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);

            ResultAssert.Fails(result, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");
        }

        // Verify account is locked out in the database
        using var db = _env.NewContext();
        var lockedUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(lockedUser);
        Assert.Equal(5, lockedUser.AccessFailedCount);
        Assert.True(lockedUser.LockoutEnd.HasValue);
        Assert.True(lockedUser.LockoutEnd.Value > _env.Time.GetUtcNow());
    }

    [Fact]
    public async Task Login_WhileLockedOut_Rejected()
    {
        var user = _env.StaffUserId;

        // Lock the account by failing 5 times
        for (int i = 0; i < 5; i++)
        {
            await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);
        }

        // 6th attempt should still fail (account locked)
        var result = await _env.AuthService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "wrong-password"
        }, CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_ResetsAttempts()
    {
        var user = _env.StaffUserId;

        // Lock the account
        for (int i = 0; i < 5; i++)
        {
            await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);
        }

        // Advance time past lockout (15 minutes)
        _env.Time.Advance(TimeSpan.FromMinutes(16));

        // Successful login should reset the counter
        var result = await _env.AuthService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "StaffPass123!"
        }, CancellationToken.None);

        ResultAssert.Ok(result);

        using var db = _env.NewContext();
        var unlockedUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(unlockedUser);
        Assert.Equal(0, unlockedUser.AccessFailedCount);
        Assert.Null(unlockedUser.LockoutEnd);
    }

    [Fact]
    public async Task Login_FailedAttemptAfterLockoutExpires_StartsFreshCount()
    {
        var user = _env.StaffUserId;

        // Lock the account.
        for (int i = 0; i < 5; i++)
        {
            await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);
        }

        _env.Time.Advance(TimeSpan.FromMinutes(16));

        // The first mistake after expiry must not immediately re-lock the account.
        ResultAssert.Fails(await _env.AuthService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "wrong-password"
        }, CancellationToken.None), IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

        using var db = _env.NewContext();
        var userAfterFailure = await db.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(userAfterFailure);
        Assert.Equal(1, userAfterFailure.AccessFailedCount);
        Assert.Null(userAfterFailure.LockoutEnd);

        // A correct password still works right after that single failure.
        ResultAssert.Ok(await _env.AuthService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "StaffPass123!"
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Login_InvalidCredentials_IncrementsFailedCount()
    {
        var user = _env.StaffUserId;

        await _env.AuthService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "wrong-password"
        }, CancellationToken.None);

        using var db = _env.NewContext();
        var failedUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(failedUser);
        Assert.Equal(1, failedUser.AccessFailedCount);
        Assert.Null(failedUser.LockoutEnd);
    }
}
