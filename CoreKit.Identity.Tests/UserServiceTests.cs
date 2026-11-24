using CoreKit.Identity.Repositories;
using CoreKit.Identity.Security;
using CoreKit.Identity.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoreKit.Identity.Tests;

/// <summary>
/// What this protects: creating, changing and removing users -- including the administrator
/// protections that prevent locking everybody out. Authorization is exercised through the host's
/// <see cref="IIdentityAdminAuthorizer"/> port (faked in <see cref="TestEnv"/>) rather than Access
/// permissions. Platform roles and membership are owned by Access and are no longer part of
/// Identity's user-management surface.
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
            Mobile = "+15551000010",
            DisplayName = "New User",
            IsActive = true
        });

        var dto = ResultAssert.Ok(result);
        Assert.Equal("newuser@example.com", dto.Email);
        Assert.Equal("+15551000010", dto.Mobile);
        Assert.Equal("New User", dto.DisplayName);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "OWNER@example.com",
            Password = "DupPass123!",
            Mobile = "+15551000011",
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
            Password = "BadEmailPass123!",
            Mobile = "+15551000012"
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
            Password = "short",
            Mobile = "+15551000013"
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
            Password = "same@example.com",
            Mobile = "+15551000014"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    // ---- Mobile validation on create ----

    [Fact]
    public async Task CreateUser_WithoutMobile_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "nomobile@example.com",
            Password = "NoMobilePass123!"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    [Fact]
    public async Task CreateUser_WithInvalidMobile_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "badmobile@example.com",
            Password = "BadMobilePass123!",
            Mobile = "not-a-phone"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    [Fact]
    public async Task CreateUser_WithDuplicateMobile_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "dup-mobile@example.com",
            Password = "DupMobilePass123!",
            Mobile = "+15551000002"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Conflict, "conflict");
    }

    [Fact]
    public async Task UserRepository_DuplicateMobile_ReturnsFalseWithoutThrowing()
    {
        using var db = _env.NewContext();
        var repo = new UserRepository(db);

        var userA = new User
        {
            Id = Guid.NewGuid(),
            Email = "repo-user-a@example.com",
            NormalizedEmail = IdentityNormalizer.NormalizeEmail("repo-user-a@example.com"),
            Mobile = "+15551000077",
            NormalizedMobile = "+15551000077",
            PasswordHash = _env.Hasher.Hash("Pass123!"),
            IsActive = true,
            CreatedAt = _env.Time.GetUtcNow().UtcDateTime
        };

        Assert.True(await repo.CreateAsync(userA, CancellationToken.None));

        var userB = new User
        {
            Id = Guid.NewGuid(),
            Email = "repo-user-b@example.com",
            NormalizedEmail = IdentityNormalizer.NormalizeEmail("repo-user-b@example.com"),
            Mobile = "+15551000077",
            NormalizedMobile = "+15551000077",
            PasswordHash = _env.Hasher.Hash("Pass123!"),
            IsActive = true,
            CreatedAt = _env.Time.GetUtcNow().UtcDateTime
        };

        Assert.False(await repo.CreateAsync(userB, CancellationToken.None));
    }

    [Fact]
    public async Task UserRepository_DuplicateEmail_ReturnsFalseWithoutThrowing()
    {
        using var db = _env.NewContext();
        var repo = new UserRepository(db);

        var userA = new User
        {
            Id = Guid.NewGuid(),
            Email = "repo-email-a@example.com",
            NormalizedEmail = IdentityNormalizer.NormalizeEmail("repo-email-a@example.com"),
            Mobile = "+15551000080",
            NormalizedMobile = "+15551000080",
            PasswordHash = _env.Hasher.Hash("Pass123!"),
            IsActive = true,
            CreatedAt = _env.Time.GetUtcNow().UtcDateTime
        };

        Assert.True(await repo.CreateAsync(userA, CancellationToken.None));

        var userB = new User
        {
            Id = Guid.NewGuid(),
            Email = "repo-email-a@example.com",
            NormalizedEmail = IdentityNormalizer.NormalizeEmail("repo-email-a@example.com"),
            Mobile = "+15551000081",
            NormalizedMobile = "+15551000081",
            PasswordHash = _env.Hasher.Hash("Pass123!"),
            IsActive = true,
            CreatedAt = _env.Time.GetUtcNow().UtcDateTime
        };

        Assert.False(await repo.CreateAsync(userB, CancellationToken.None));
    }

    [Fact]
    public async Task CreateUser_WithAlphabeticMobile_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "alpha-mobile@example.com",
            Password = "AlphaMobilePass123!",
            Mobile = "abc1234567"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    [Fact]
    public async Task CreateUser_WithInternalPlusMobile_Fails()
    {
        _env.SignInOwner();

        var result = await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "plus-mobile@example.com",
            Password = "PlusMobilePass123!",
            Mobile = "1+2345678"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateUser_WithOwnMobile_Succeeds()
    {
        _env.SignInOwner();

        var result = await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Mobile = "+15551000002"
        });

        var dto = ResultAssert.Ok(result);
        Assert.Equal("+15551000002", dto.Mobile);
    }

    [Fact]
    public async Task UpdateUser_WithOwnMobileFormattedDifferently_Succeeds()
    {
        _env.SignInOwner();

        var result = await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Mobile = "+1 555 100 0002"
        });

        var dto = ResultAssert.Ok(result);
        Assert.Equal("+1 555 100 0002", dto.Mobile);

        using var db = _env.NewContext();
        var user = await db.Users.FindAsync(_env.StaffUserId);
        Assert.NotNull(user);
        Assert.Equal("+15551000002", user.NormalizedMobile);
    }

    // ---- Get ----

    [Fact]
    public async Task GetUser_ThatExists_ReturnsIt()
    {
        _env.SignInOwner();

        var dto = ResultAssert.Ok(await _env.UserService.GetAsync(_env.StaffUserId));

        Assert.Equal("staff@example.com", dto.Email);
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
    public async Task UpdateUser_Email_IsRejected()
    {
        _env.SignInOwner();

        var result = await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Email = "newstaff@example.com"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");

        using var db = _env.NewContext();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == _env.StaffUserId);
        Assert.NotNull(user);
        Assert.Equal("staff@example.com", user.Email);
        Assert.Equal(IdentityNormalizer.NormalizeEmail("staff@example.com"), user.NormalizedEmail);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task UpdateUser_CasingOnlyEmailChange_IsRejected()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Email = "STAFF@example.com"
        }), IdentityErrorKind.Validation, "validation.failed");

        using var db = _env.NewContext();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == _env.StaffUserId);
        Assert.NotNull(user);
        Assert.Equal("staff@example.com", user.Email);
    }

    [Fact]
    public async Task UpdateUser_IdenticalEmail_IsRejected()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Email = "staff@example.com"
        }), IdentityErrorKind.Validation, "validation.failed");
    }

    [Fact]
    public async Task UpdateUser_EmailWithOtherFields_IsRejectedEntirely()
    {
        _env.SignInOwner();

        var result = await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Email = "newstaff@example.com",
            DisplayName = "New Display Name"
        });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "validation.failed");

        using var db = _env.NewContext();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == _env.StaffUserId);
        Assert.NotNull(user);
        Assert.Equal("staff@example.com", user.Email);
        Assert.NotEqual("New Display Name", user.DisplayName);
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

        using var db = _env.NewContext();
        var user = await db.Users.FindAsync(_env.StaffUserId);
        Assert.NotNull(user);
        Assert.NotEqual("StaffPass123!", user.PasswordHash);
    }

    [Fact]
    public async Task UpdateUser_Mobile_Succeeds()
    {
        _env.SignInOwner();

        var result = await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Mobile = "+15551000020"
        });

        var dto = ResultAssert.Ok(result);
        Assert.Equal("+15551000020", dto.Mobile);

        using var db = _env.NewContext();
        var user = await db.Users.FindAsync(_env.StaffUserId);
        Assert.NotNull(user);
        Assert.Equal("+15551000020", user.Mobile);
        Assert.Equal("+15551000020", user.NormalizedMobile);
    }

    [Fact]
    public async Task UpdateUser_WithInvalidMobile_Fails()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Mobile = "not-a-phone"
        }), IdentityErrorKind.Validation, "validation.failed");
    }

    [Fact]
    public async Task UpdateUser_WithDuplicateMobile_Fails()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Mobile = "+15551000001"
        }), IdentityErrorKind.Conflict, "conflict");
    }

    [Fact]
    public async Task UpdateUser_WithDuplicateEmail_Fails()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Email = "owner@example.com"
        }), IdentityErrorKind.Validation, "validation.failed");

        using var db = _env.NewContext();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == _env.StaffUserId);
        Assert.NotNull(user);
        Assert.Equal("staff@example.com", user.Email);
    }

    [Fact]
    public async Task UpdateUser_MobileClearedWhenSetToEmpty_Fails()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            Mobile = string.Empty
        }), IdentityErrorKind.Validation, "validation.failed");
    }

    [Fact]
    public async Task UpdateUser_WithoutMobile_LeavesExistingMobileUnchanged()
    {
        _env.SignInOwner();

        var result = await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            DisplayName = "New Display Name"
        });

        var dto = ResultAssert.Ok(result);
        Assert.Equal("+15551000002", dto.Mobile);
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
    public async Task UpdateUser_DeleteYourself_Fails()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.UserService.DeleteAsync(_env.OwnerUserId),
            IdentityErrorKind.Forbidden, "forbidden");
    }

    // ---- Authorization ----

    [Fact]
    public async Task CreateUser_WithoutUsersCreatePermission_Fails()
    {
        _env.Authorizer.Deny(IdentityAdminOperation.CreateUsers);
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "newuser@example.com",
            Password = "NewUserPass123!",
            Mobile = "+15551000016"
        }), IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task GetUser_WithoutUsersReadPermission_Fails()
    {
        _env.Authorizer.Deny(IdentityAdminOperation.ReadUsers);
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.GetAsync(_env.StaffUserId),
            IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task ListUsers_WithoutUsersReadPermission_Fails()
    {
        _env.Authorizer.Deny(IdentityAdminOperation.ReadUsers);
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.ListAsync(new UserListQuery()),
            IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task CreateUser_WithOnlyCreatePermission_Succeeds()
    {
        _env.Authorizer.Deny(IdentityAdminOperation.ReadUsers);
        _env.Authorizer.Allow(IdentityAdminOperation.CreateUsers);
        _env.SignInStaff();

        var dto = ResultAssert.Ok(await _env.UserService.CreateAsync(new CreateUserRequest
        {
            Email = "createonly@example.com",
            Password = "CreateOnlyPass123!",
            Mobile = "+15551000017",
            DisplayName = "Create Only"
        }));

        Assert.Equal("createonly@example.com", dto.Email);
    }

    [Fact]
    public async Task UpdateUser_WithoutUsersUpdatePermission_Fails()
    {
        _env.Authorizer.Deny(IdentityAdminOperation.UpdateUsers);
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.UpdateAsync(_env.StaffUserId, new UpdateUserRequest
        {
            DisplayName = "New Display Name"
        }), IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task DeleteUser_WithoutUsersDeletePermission_Fails()
    {
        _env.Authorizer.Deny(IdentityAdminOperation.DeleteUsers);
        _env.SignInStaff();

        ResultAssert.Fails(await _env.UserService.DeleteAsync(_env.StaffUserId),
            IdentityErrorKind.Forbidden, "access.denied");
    }

    // ---- Account lockout ----

    [Fact]
    public async Task Login_FiveFailedAttempts_LocksAccountFor15Minutes()
    {
        var user = _env.StaffUserId;

        // First 4 failed attempts return InvalidCredentials
        for (int i = 0; i < 4; i++)
        {
            var result = await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);

            ResultAssert.Fails(result, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");
        }

        // 5th failed attempt locks the account and returns AccountLocked
        var result5 = await _env.AuthService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "wrong-password"
        }, CancellationToken.None);

        ResultAssert.Fails(result5, IdentityErrorKind.Forbidden, "auth.account_locked");

        using var db = _env.NewContext();
        var lockedUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(lockedUser);
        Assert.Equal(5, lockedUser.AccessFailedCount);
        Assert.True(lockedUser.LockoutEnd.HasValue);
        var expectedLockoutEnd = _env.Time.GetUtcNow().UtcDateTime.AddMinutes(15);
        Assert.Equal(expectedLockoutEnd.Ticks, lockedUser.LockoutEnd.Value.Ticks);
    }

    [Fact]
    public async Task Login_WhileLockedOut_ReturnsAccountLocked()
    {
        var user = _env.StaffUserId;

        for (int i = 0; i < 5; i++)
        {
            await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);
        }

        var result = await _env.AuthService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "wrong-password"
        }, CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrorKind.Forbidden, "auth.account_locked");
    }

    [Fact]
    public async Task Login_ValidCredentialsDuringLockout_ReturnsAccountLocked()
    {
        var user = _env.StaffUserId;

        for (int i = 0; i < 5; i++)
        {
            await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);
        }

        using var lockedDb = _env.NewContext();
        var lockedUser = await lockedDb.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(lockedUser);
        var lockoutEnd = lockedUser!.LockoutEnd;
        Assert.True(lockoutEnd.HasValue);

        var result = await _env.AuthService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "StaffPass123!"
        }, CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrorKind.Forbidden, "auth.account_locked");

        using var db = _env.NewContext();
        var userAfter = await db.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(userAfter);
        // Correct password on locked account: TryCompleteSuccessfulLoginAsync returns Locked,
        // we do NOT call RegisterFailedLoginAttemptAsync, so counter stays at 5
        Assert.Equal(5, userAfter.AccessFailedCount);
        Assert.Equal(lockoutEnd, userAfter.LockoutEnd);
    }

    [Fact]
    public async Task Login_FourthFailureNoLock_FifthFailureLocks()
    {
        var user = _env.StaffUserId;

        for (int i = 0; i < 4; i++)
        {
            await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);
        }

        using var db4 = _env.NewContext();
        var user4 = await db4.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(user4);
        Assert.Equal(4, user4.AccessFailedCount);
        Assert.Null(user4.LockoutEnd);

        await _env.AuthService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "wrong-password"
        }, CancellationToken.None);

        using var db5 = _env.NewContext();
        var user5 = await db5.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(user5);
        Assert.Equal(5, user5.AccessFailedCount);
        Assert.True(user5.LockoutEnd.HasValue);
        Assert.True(user5.LockoutEnd.Value > _env.Time.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task Login_LockedAttempt_ReturnsAccountLocked()
    {
        var user = _env.StaffUserId;

        for (int i = 0; i < 5; i++)
        {
            await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);
        }

        using var lockedDb = _env.NewContext();
        var lockedUser = await lockedDb.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(lockedUser);
        var originalLockoutEnd = lockedUser!.LockoutEnd;
        Assert.True(originalLockoutEnd.HasValue);

        var result = await _env.AuthService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "wrong-password"
        }, CancellationToken.None);

        ResultAssert.Fails(result, IdentityErrorKind.Forbidden, "auth.account_locked");

        using var db = _env.NewContext();
        var userAfter = await db.Users.FirstOrDefaultAsync(u => u.Id == user);
        Assert.NotNull(userAfter);
        // Counter should NOT increment when account is already locked (early return)
        Assert.Equal(5, userAfter.AccessFailedCount);
        Assert.Equal(originalLockoutEnd, userAfter.LockoutEnd);
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_ResetsAttempts()
    {
        var user = _env.StaffUserId;

        for (int i = 0; i < 5; i++)
        {
            await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);
        }

        _env.Time.Advance(TimeSpan.FromMinutes(16));

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

        for (int i = 0; i < 5; i++)
        {
            await _env.AuthService.LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None);
        }

        _env.Time.Advance(TimeSpan.FromMinutes(16));

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

    [Fact]
    public async Task Login_ConcurrentFailedAttempts_DoNotLoseIncrements()
    {
        var email = "staff@example.com";
        var wrongPassword = "wrong-password";
        var attemptCount = 10;

        var services = Enumerable.Range(0, attemptCount)
            .Select(_ => CreateAuthService())
            .ToList();

        var tasks = services.Select(s => s.LoginAsync(
            new LoginRequest { Email = email, Password = wrongPassword },
            CancellationToken.None)).ToList();

        await Task.WhenAll(tasks);

        // Each request may return InvalidCredentials (before lockout) or AccountLocked (after lockout established)
        foreach (var task in tasks)
        {
            var result = await task;
            Assert.True(result.IsFailure);
            Assert.Contains(result.Error!.Kind, new[] { IdentityErrorKind.Unauthorized, IdentityErrorKind.Forbidden });
            if (result.Error.Kind == IdentityErrorKind.Forbidden)
                Assert.Equal("auth.account_locked", result.Error.Code);
            else
                Assert.Equal("auth.invalid_credentials", result.Error.Code);
        }

        using var db = _env.NewContext();
        var userAfter = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == IdentityNormalizer.NormalizeEmail(email));
        Assert.NotNull(userAfter);
        // Only 5 increments: first 5 attempts before lockout; remaining 5 return AccountLocked early without incrementing
        Assert.Equal(5, userAfter.AccessFailedCount);
        Assert.True(userAfter.LockoutEnd.HasValue);
    }

    [Fact]
    public async Task RegisterFailedLoginAttemptAsync_WithCancelledToken_ThrowsAndDoesNotApplyTransition()
    {
        using var db = _env.NewContext();
        var repo = new UserRepository(db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var now = _env.Time.GetUtcNow().UtcDateTime;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repo.RegisterFailedLoginAttemptAsync(
                _env.StaffUserId, now, now.AddMinutes(15), 5, cts.Token));

        using var verify = _env.NewContext();
        var user = await verify.Users.FirstOrDefaultAsync(u => u.Id == _env.StaffUserId);
        Assert.NotNull(user);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task TryCompleteSuccessfulLoginAsync_WithCancelledToken_ThrowsAndDoesNotApplyTransition()
    {
        using var db = _env.NewContext();
        var repo = new UserRepository(db);
        var now = _env.Time.GetUtcNow().UtcDateTime;

        for (var i = 0; i < 3; i++)
            Assert.True(await repo.RegisterFailedLoginAttemptAsync(
                _env.StaffUserId, now, now.AddMinutes(15), 5));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repo.TryCompleteSuccessfulLoginAsync(_env.StaffUserId, 0, now, cts.Token));

        using var verify = _env.NewContext();
        var user = await verify.Users.FirstOrDefaultAsync(u => u.Id == _env.StaffUserId);
        Assert.NotNull(user);
        Assert.Equal(3, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task FailedLoginTransition_StaleExpiredLockoutRequest_CannotClearNewerLockout()
    {
        using var db = _env.NewContext();
        var repo = new UserRepository(db);
        var userId = _env.StaffUserId;

        var staleNow = _env.Time.GetUtcNow().UtcDateTime;
        var newerNow = staleNow.AddMinutes(1);

        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.AccessFailedCount, 5)
                .SetProperty(u => u.LockoutEnd, (DateTime?)staleNow.AddMinutes(-1)));

        for (var i = 0; i < 5; i++)
            Assert.True(await repo.RegisterFailedLoginAttemptAsync(
                userId, newerNow, newerNow.AddMinutes(15), 5));

        Assert.True(await repo.RegisterFailedLoginAttemptAsync(
            userId, staleNow, staleNow.AddMinutes(15), 5));

        using var verify = _env.NewContext();
        var user = await verify.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        Assert.NotNull(user);
        Assert.Equal(newerNow.AddMinutes(15).Ticks, user.LockoutEnd!.Value.Ticks);
        Assert.Equal(6, user.AccessFailedCount);
    }

    [Fact]
    public async Task SuccessfulLoginTransition_StaleRequest_CannotClearNewerActiveLockout()
    {
        using var db = _env.NewContext();
        var repo = new UserRepository(db);
        var userId = _env.StaffUserId;

        var staleNow = _env.Time.GetUtcNow().UtcDateTime;
        var newerNow = staleNow.AddMinutes(1);

        for (var i = 0; i < 5; i++)
            Assert.True(await repo.RegisterFailedLoginAttemptAsync(
                userId, newerNow, newerNow.AddMinutes(15), 5));

        Assert.Equal(LoginCompletion.Locked, await repo.TryCompleteSuccessfulLoginAsync(userId, 0, staleNow));

        using var verify = _env.NewContext();
        var user = await verify.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        Assert.NotNull(user);
        Assert.Equal(newerNow.AddMinutes(15).Ticks, user.LockoutEnd!.Value.Ticks);
        Assert.Equal(5, user.AccessFailedCount);
    }

    [Fact]
    public async Task SuccessfulLoginTransition_InactiveAccount_IsRefused()
    {
        using var db = _env.NewContext();
        var repo = new UserRepository(db);

        Assert.Equal(LoginCompletion.Inactive, await repo.TryCompleteSuccessfulLoginAsync(
            _env.InactiveUserId, 0, _env.Time.GetUtcNow().UtcDateTime));

        using var verify = _env.NewContext();
        var user = await verify.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == _env.InactiveUserId);
        Assert.NotNull(user);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task Login_CorrectPassword_WhenConcurrentFailureLocksFirst_DoesNotIssueSession()
    {
        var atTransition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTransition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gatedRepo = new GatedUserRepository(
            new UserRepository(_env.NewContext()),
            async () =>
            {
                atTransition.SetResult();
                await releaseTransition.Task;
            });
        var authService = CreateAuthService(gatedRepo);

        var loginTask = authService.LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "StaffPass123!"
        }, CancellationToken.None);

        await atTransition.Task;

        // First 4 failed attempts return InvalidCredentials
        for (var i = 0; i < 4; i++)
        {
            ResultAssert.Fails(await CreateAuthService().LoginAsync(new LoginRequest
            {
                Email = "staff@example.com",
                Password = "wrong-password"
            }, CancellationToken.None), IdentityErrorKind.Unauthorized, "auth.invalid_credentials");
        }

        // 5th failed attempt locks the account and returns AccountLocked
        ResultAssert.Fails(await CreateAuthService().LoginAsync(new LoginRequest
        {
            Email = "staff@example.com",
            Password = "wrong-password"
        }, CancellationToken.None), IdentityErrorKind.Forbidden, "auth.account_locked");

        releaseTransition.SetResult();

        // The correct-password login should now fail with AccountLocked (not InvalidCredentials)
        ResultAssert.Fails(await loginTask, IdentityErrorKind.Forbidden, "auth.account_locked");

        using var verify = _env.NewContext();
        var user = await verify.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == IdentityNormalizer.NormalizeEmail("staff@example.com"));
        Assert.NotNull(user);
        // Counter should be 5 (the 5 failed attempts), not 6 (the locked attempt doesn't increment)
        Assert.Equal(5, user.AccessFailedCount);
        Assert.NotNull(user.LockoutEnd);
    }

    private IAuthService CreateAuthService()
        => CreateAuthService(userRepository: null);

    private IAuthService CreateAuthService(IUserRepository? userRepository)
    {
        var db = _env.NewContext();
        userRepository ??= new UserRepository(db);
        var refreshTokenRepository = new RefreshTokenRepository(db);
        var currentUser = new FakeCurrentUserService();
        var emailOptions = Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false });

        return new AuthService(
            userRepository,
            refreshTokenRepository,
            _env.Hasher,
            new FakeTokenService(),
            new FakeSessionIssuer(),
            currentUser,
            emailOptions,
            _env.LockoutOptions,
            _env.Time,
            NullLogger<AuthService>.Instance);
    }
}
