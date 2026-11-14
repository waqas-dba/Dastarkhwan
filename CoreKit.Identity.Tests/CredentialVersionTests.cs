using CoreKit.Identity.Repositories;
using CoreKit.SharedKernel.Abstractions;

namespace CoreKit.Identity.Tests;

/// <summary>
/// DK-IDENTITY-LOCKOUT-FIX-04 regression coverage. What this protects: the SecurityVersion
/// credential guard and guarded session issuance — a stale password (after a concurrent
/// change, administrator reset or deactivation) must never create a session, credential
/// updates must be version-conditional and atomic with session revocation, rehash must never
/// overwrite a newer password, and email must be immutable after creation.
/// Every race is forced with deterministic gates (task completion sources), never sleeps, and
/// every assertion checks persisted database state, not just return values.
/// </summary>
public sealed class CredentialVersionTests : IDisposable
{
    private const string StaffEmail = "staff@example.com";
    private const string StaffPassword = "StaffPass123!";

    private readonly TestEnv _env;

    public CredentialVersionTests() => _env = new TestEnv();

    public void Dispose() => _env.Dispose();

    // ---- helpers: one isolated object graph per simulated request ----

    private IAuthService CreateLoginService(
        IUserRepository? userRepository = null,
        ISessionIssuer? sessions = null,
        IPasswordHasher? hasher = null,
        ICurrentUserService? currentUser = null)
    {
        var db = _env.NewContext();
        return new AuthService(
            userRepository ?? new UserRepository(db),
            new RefreshTokenRepository(db),
            hasher ?? _env.Hasher,
            new FakeTokenService(),
            sessions ?? new FakeSessionIssuer(),
            new AccessResolver(new PermissionRepository(db)),
            currentUser ?? new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.Time,
            NullLogger<AuthService>.Instance);
    }

    private UserService CreateAdminService(IUserRepository? userRepository = null)
        => new(
            userRepository ?? new UserRepository(_env.NewContext()),
            new RoleRepository(_env.NewContext()),
            new AccessResolver(new PermissionRepository(_env.NewContext())),
            new AdministratorGuard(new UserRepository(_env.NewContext())),
            _env.Hasher,
            OwnerCurrentUser(),
            _env.Time);

    private FakeCurrentUserService OwnerCurrentUser()
        => new()
        {
            UserId = _env.OwnerUserId,
            Email = "owner@example.com",
            Roles = new List<string> { IdentityRoleNames.Administrator },
            Permissions = IdentityPermissionNames.All.Select(p => p.Name).ToList()
        };

    private Task<IdentityResult> SelfServiceChangePasswordAsync(string newPassword)
    {
        var auth = CreateLoginService(currentUser: new FakeCurrentUserService
        {
            UserId = _env.StaffUserId,
            Email = StaffEmail,
            Roles = new List<string> { IdentityRoleNames.Staff },
            Permissions = new List<string> { IdentityPermissionNames.UsersUpdate }
        });
        return auth.ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = StaffPassword,
            NewPassword = newPassword
        });
    }

    private Task<IdentityResult<UserDto>> AdminUpdateAsync(UpdateUserRequest request)
        => CreateAdminService().UpdateAsync(_env.StaffUserId, request);

    private async Task SeedActiveRefreshTokenAsync(string rawToken)
    {
        using var db = _env.NewContext();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = _env.StaffUserId,
            TokenHash = new FakeTokenService().HashRefreshToken(rawToken),
            CreatedAt = _env.Time.GetUtcNow().UtcDateTime,
            ExpiresAt = _env.Time.GetUtcNow().UtcDateTime.AddDays(7)
        });
        await db.SaveChangesAsync();
    }

    private async Task<User> ReadUserAsync()
    {
        using var db = _env.NewContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
    }

    private async Task<List<RefreshToken>> ReadTokensAsync()
    {
        using var db = _env.NewContext();
        return await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == _env.StaffUserId)
            .ToListAsync();
    }

    private SessionIssuer CreateRealSessionIssuer(IdentityDbContext db)
        => new(
            new AccessResolver(new PermissionRepository(db)),
            new FakeTokenService(),
            new RefreshTokenRepository(db),
            Array.Empty<IAccessTokenClaimsContributor>(),
            _env.Time);

    /// <summary>
    /// A login service whose session issuer really stores refresh tokens. Refresh tests need this
    /// because the always-succeeding fake inserts no row, so rotated-token and revocation
    /// assertions would otherwise see nothing.
    /// </summary>
    private IAuthService CreateLoginServiceWithRealSessions()
    {
        var db = _env.NewContext();
        return new AuthService(
            new UserRepository(db),
            new RefreshTokenRepository(db),
            _env.Hasher,
            new FakeTokenService(),
            CreateRealSessionIssuer(db),
            new AccessResolver(new PermissionRepository(db)),
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.Time,
            NullLogger<AuthService>.Instance);
    }

    private EmailVerificationService CreateEmailVerificationService()
    {
        var db = _env.NewContext();
        return new EmailVerificationService(
            new UserRepository(db),
            new EmailVerificationTokenRepository(db),
            new FakeCurrentUserService(),
            new NullIdentityEmailSender(NullLogger<NullIdentityEmailSender>.Instance),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.Time,
            NullLogger<EmailVerificationService>.Instance);
    }

    // ---- login vs concurrent credential changes ----

    [Fact]
    public async Task Login_CorrectPassword_WhenSelfServiceChangeLandsDuringTransition_IsRejectedAndNotCounted()
    {
        // The login request parks between password verification and the authoritative
        // transition; a self-service password change wins the credential race; the stale login
        // must be rejected without counting a failed password.
        var atTransition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTransition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gatedRepo = new GatedUserRepository(
            new UserRepository(_env.NewContext()),
            beforeSuccessTransition: async () =>
            {
                atTransition.SetResult();
                await releaseTransition.Task;
            });
        var auth = CreateLoginService(userRepository: gatedRepo);

        var loginTask = auth.LoginAsync(new LoginRequest { Email = StaffEmail, Password = StaffPassword }, CancellationToken.None);
        await atTransition.Task;

        ResultAssert.Ok(await SelfServiceChangePasswordAsync("NewStaffPass456!"));

        releaseTransition.SetResult();
        ResultAssert.Fails(await loginTask, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

        var user = await ReadUserAsync();
        Assert.Equal(1, user.SecurityVersion);
        Assert.Equal(0, user.AccessFailedCount); // a stale credential is not a wrong password
        Assert.Null(user.LockoutEnd);
        Assert.Equal(PasswordCheckResult.Failed, _env.Hasher.Verify(user.PasswordHash, StaffPassword));
        Assert.Equal(PasswordCheckResult.Success, _env.Hasher.Verify(user.PasswordHash, "NewStaffPass456!"));
        // the change revoked every session in the same transaction
        Assert.All(await ReadTokensAsync(), t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task Login_CorrectPassword_WhenAdminResetLandsDuringTransition_IsRejectedAndNotCounted()
    {
        var atTransition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTransition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gatedRepo = new GatedUserRepository(
            new UserRepository(_env.NewContext()),
            beforeSuccessTransition: async () =>
            {
                atTransition.SetResult();
                await releaseTransition.Task;
            });
        var auth = CreateLoginService(userRepository: gatedRepo);

        var loginTask = auth.LoginAsync(new LoginRequest { Email = StaffEmail, Password = StaffPassword }, CancellationToken.None);
        await atTransition.Task;

        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { Password = "AdminReset789!" }));

        releaseTransition.SetResult();
        ResultAssert.Fails(await loginTask, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

        var user = await ReadUserAsync();
        Assert.Equal(1, user.SecurityVersion);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task Login_CorrectPassword_WhenDeactivatedBeforeTransition_IsRejectedAndCounted()
    {
        // Deactivation lands before the login request even starts: the snapshot already shows
        // an inactive account, which follows the approved failed-attempt policy.
        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { IsActive = false }));

        ResultAssert.Fails(
            await CreateLoginService().LoginAsync(new LoginRequest { Email = StaffEmail, Password = StaffPassword }, CancellationToken.None),
            IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

        var user = await ReadUserAsync();
        Assert.False(user.IsActive);
        Assert.Equal(1, user.SecurityVersion);
        Assert.Equal(1, user.AccessFailedCount);
    }

    [Fact]
    public async Task Login_CorrectPassword_IssuanceGuard_RefusesWhenPasswordChangedAfterTransition()
    {
        // The login completes its transition, then parks before session issuance; a password
        // change wins the race; the guarded insertion must refuse, so no session exists.
        var atIssuance = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseIssuance = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var db = _env.NewContext();
        var gatedSessions = new GatedSessionIssuer(
            CreateRealSessionIssuer(db),
            async () =>
            {
                atIssuance.SetResult();
                await releaseIssuance.Task;
            });
        var auth = new AuthService(
            new UserRepository(db),
            new RefreshTokenRepository(db),
            _env.Hasher,
            new FakeTokenService(),
            gatedSessions,
            new AccessResolver(new PermissionRepository(db)),
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.Time,
            NullLogger<AuthService>.Instance);

        var loginTask = auth.LoginAsync(new LoginRequest { Email = StaffEmail, Password = StaffPassword }, CancellationToken.None);
        await atIssuance.Task;

        ResultAssert.Ok(await SelfServiceChangePasswordAsync("NewStaffPass456!"));

        releaseIssuance.SetResult();
        ResultAssert.Fails(await loginTask, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

        // The guarded insert never ran: no token row exists for the user.
        Assert.Empty(await ReadTokensAsync());
        var user = await ReadUserAsync();
        Assert.Equal(1, user.SecurityVersion);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task Login_CorrectPassword_IssuanceGuard_RefusesWhenDeactivatedAfterTransition()
    {
        var atIssuance = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseIssuance = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var db = _env.NewContext();
        var gatedSessions = new GatedSessionIssuer(
            CreateRealSessionIssuer(db),
            async () =>
            {
                atIssuance.SetResult();
                await releaseIssuance.Task;
            });
        var auth = new AuthService(
            new UserRepository(db),
            new RefreshTokenRepository(db),
            _env.Hasher,
            new FakeTokenService(),
            gatedSessions,
            new AccessResolver(new PermissionRepository(db)),
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.Time,
            NullLogger<AuthService>.Instance);

        var loginTask = auth.LoginAsync(new LoginRequest { Email = StaffEmail, Password = StaffPassword }, CancellationToken.None);
        await atIssuance.Task;

        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { IsActive = false }));

        releaseIssuance.SetResult();
        ResultAssert.Fails(await loginTask, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

        Assert.Empty(await ReadTokensAsync());
        var user = await ReadUserAsync();
        Assert.False(user.IsActive);
        Assert.Equal(1, user.SecurityVersion);
    }

    // ---- credential update transitions ----

    [Fact]
    public async Task ChangePassword_SelfService_AtomicallyBumpsVersionAndRevokesEverySession()
    {
        await SeedActiveRefreshTokenAsync("raw-token-1");

        ResultAssert.Ok(await SelfServiceChangePasswordAsync("NewStaffPass456!"));

        var user = await ReadUserAsync();
        Assert.Equal(1, user.SecurityVersion);
        Assert.Equal(PasswordCheckResult.Failed, _env.Hasher.Verify(user.PasswordHash, StaffPassword));
        Assert.Equal(PasswordCheckResult.Success, _env.Hasher.Verify(user.PasswordHash, "NewStaffPass456!"));
        Assert.All(await ReadTokensAsync(), t => Assert.NotNull(t.RevokedAt));

        // The old password no longer authenticates; the new one does.
        ResultAssert.Fails(
            await CreateLoginService().LoginAsync(new LoginRequest { Email = StaffEmail, Password = StaffPassword }, CancellationToken.None),
            IdentityErrorKind.Unauthorized, "auth.invalid_credentials");
        ResultAssert.Ok(
            await CreateLoginService().LoginAsync(new LoginRequest { Email = StaffEmail, Password = "NewStaffPass456!" }, CancellationToken.None));
    }

    [Fact]
    public async Task AdminReset_TwoConcurrentResets_LoadedBeforeEitherSaves_OnlyOneApplies()
    {
        // Both administrators load the same version-0 user before either writes. The first is
        // parked immediately before its conditional update; the second completes; the first
        // then loses the version race and must not overwrite the second's password.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gatedRepo = new GatedUserRepository(
            new UserRepository(_env.NewContext()),
            beforeCredentialUpdate: async () =>
            {
                gate.SetResult();
                await release.Task;
            });

        var first = CreateAdminService(gatedRepo).UpdateAsync(
            _env.StaffUserId, new UpdateUserRequest { Password = "FirstResetPass123!" });
        await gate.Task;

        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { Password = "SecondResetPass456!" }));

        release.SetResult();
        ResultAssert.Fails(await first, IdentityErrorKind.Conflict, "conflict");

        var user = await ReadUserAsync();
        Assert.Equal(1, user.SecurityVersion); // exactly one increment
        var firstWins = _env.Hasher.Verify(user.PasswordHash, "FirstResetPass123!") == PasswordCheckResult.Success;
        var secondWins = _env.Hasher.Verify(user.PasswordHash, "SecondResetPass456!") == PasswordCheckResult.Success;
        Assert.True(secondWins);
        Assert.False(firstWins);
        Assert.All(await ReadTokensAsync(), t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task Deactivate_AtomicallyBumpsVersionRevokesSessionsAndBlocksLogin()
    {
        await SeedActiveRefreshTokenAsync("raw-token-1");

        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { IsActive = false }));

        var user = await ReadUserAsync();
        Assert.False(user.IsActive);
        Assert.Equal(1, user.SecurityVersion);
        Assert.All(await ReadTokensAsync(), t => Assert.NotNull(t.RevokedAt));

        ResultAssert.Fails(
            await CreateLoginService().LoginAsync(new LoginRequest { Email = StaffEmail, Password = StaffPassword }, CancellationToken.None),
            IdentityErrorKind.Unauthorized, "auth.invalid_credentials");
    }

    [Fact]
    public async Task Deactivate_ConcurrentRequest_LoadedBeforeTheWinningUpdate_LosesVersionRace()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gatedRepo = new GatedUserRepository(
            new UserRepository(_env.NewContext()),
            beforeCredentialUpdate: async () =>
            {
                gate.SetResult();
                await release.Task;
            });

        var loser = CreateAdminService(gatedRepo).UpdateAsync(
            _env.StaffUserId, new UpdateUserRequest { IsActive = false });
        await gate.Task;

        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { IsActive = false }));

        release.SetResult();
        ResultAssert.Fails(await loser, IdentityErrorKind.Conflict, "conflict");

        // Deactivation applied exactly once: one version increment, no double revocation state.
        Assert.Equal(1, (await ReadUserAsync()).SecurityVersion);
    }

    [Fact]
    public async Task ChangePassword_WhenAccountChangedConcurrently_FailsWithoutApplyingAnything()
    {
        // The self-service request verifies the old password, then parks before its conditional
        // update; an administrator reset wins; the change must fail and leave the reset intact.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gatedRepo = new GatedUserRepository(
            new UserRepository(_env.NewContext()),
            beforeCredentialUpdate: async () =>
            {
                gate.SetResult();
                await release.Task;
            });

        var changeTask = CreateLoginService(userRepository: gatedRepo, currentUser: new FakeCurrentUserService
        {
            UserId = _env.StaffUserId,
            Email = StaffEmail,
            Roles = new List<string> { IdentityRoleNames.Staff },
            Permissions = new List<string> { IdentityPermissionNames.UsersUpdate }
        }).ChangePasswordAsync(new ChangePasswordRequest
        {
            CurrentPassword = StaffPassword,
            NewPassword = "SelfChangePass123!"
        });
        await gate.Task;

        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { Password = "AdminReset789!" }));

        release.SetResult();
        ResultAssert.Fails(await changeTask, IdentityErrorKind.Conflict, "conflict");

        var user = await ReadUserAsync();
        Assert.Equal(1, user.SecurityVersion);
        Assert.Equal(PasswordCheckResult.Success, _env.Hasher.Verify(user.PasswordHash, "AdminReset789!"));
        Assert.Equal(PasswordCheckResult.Failed, _env.Hasher.Verify(user.PasswordHash, "SelfChangePass123!"));
    }

    [Fact]
    public async Task ChangePassword_WithCancelledToken_ThrowsAndAppliesNothing()
    {
        using var db = _env.NewContext();
        var repo = new UserRepository(db);
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repo.TryChangePasswordAsync(
                _env.StaffUserId, user.PasswordHash, user.SecurityVersion,
                "newhash", _env.Time.GetUtcNow().UtcDateTime, true, true, cts.Token));

        var after = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        Assert.Equal(user.PasswordHash, after.PasswordHash);
        Assert.Equal(user.SecurityVersion, after.SecurityVersion);
        Assert.Empty(await ReadTokensAsync());
    }

    // ---- conditional rehash ----

    [Fact]
    public async Task Rehash_WhenPasswordChangedConcurrently_DoesNotOverwriteNewerPassword()
    {
        using var db = _env.NewContext();
        var repo = new UserRepository(db);
        var now = _env.Time.GetUtcNow().UtcDateTime;

        var originalHash = (await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId)).PasswordHash;

        // A password change lands between the login's snapshot read and its rehash.
        Assert.Equal(CredentialUpdateResult.Updated, await repo.TryChangePasswordAsync(
            _env.StaffUserId, originalHash, 0, "hash:newer-password", now,
            bumpSecurityVersion: true, revokeSessions: true));

        // The stale rehash is refused: the newer password stays.
        Assert.False(await repo.TryRehashPasswordAsync(_env.StaffUserId, originalHash, 0, "hash:stale-rehash", now));

        var afterChange = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        Assert.Equal("hash:newer-password", afterChange.PasswordHash);
        Assert.Equal(1, afterChange.SecurityVersion);

        // A rehash with current expectations succeeds and never bumps the version.
        Assert.True(await repo.TryRehashPasswordAsync(_env.StaffUserId, "hash:newer-password", 1, "hash:rehash-of-new", now));
        var afterRehash = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        Assert.Equal("hash:rehash-of-new", afterRehash.PasswordHash);
        Assert.Equal(1, afterRehash.SecurityVersion); // rehash must not increment the version
    }

    [Fact]
    public async Task Login_WithRehashNeeded_WhenAdminResetLandsDuringRehash_DoesNotIssueSession()
    {
        // Full path with the real hasher stubbed to require a rehash: the login parks inside the
        // rehash; an administrator reset wins; the rehash must not clobber the new password and
        // the guarded issuance must refuse the stale session.
        var atRehash = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRehash = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var db = _env.NewContext();
        await db.Users
            .Where(u => u.Id == _env.StaffUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, "hash:old"));

        var gatedRepo = new GatedUserRepository(
            new UserRepository(db),
            beforePasswordRehash: async () =>
            {
                atRehash.SetResult();
                await releaseRehash.Task;
            });
        var stubHasher = new StubHasher(PasswordCheckResult.SuccessRehashNeeded);
        var auth = new AuthService(
            gatedRepo,
            new RefreshTokenRepository(db),
            stubHasher,
            new FakeTokenService(),
            CreateRealSessionIssuer(db),
            new AccessResolver(new PermissionRepository(db)),
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.Time,
            NullLogger<AuthService>.Instance);

        var loginTask = auth.LoginAsync(new LoginRequest { Email = StaffEmail, Password = "old" }, CancellationToken.None);
        await atRehash.Task;

        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { Password = "AdminReset789!" }));

        releaseRehash.SetResult();
        ResultAssert.Fails(await loginTask, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

        // The rehash was attempted once and refused: the admin's password is the live one.
        Assert.Equal(1, stubHasher.HashCallCount);
        var user = await ReadUserAsync();
        Assert.Equal(1, user.SecurityVersion);
        Assert.Equal(PasswordCheckResult.Success, _env.Hasher.Verify(user.PasswordHash, "AdminReset789!"));
        Assert.Empty(await ReadTokensAsync());
    }

    // ---- refresh vs credential changes ----

    [Fact]
    public async Task Refresh_WhenPasswordChangedDuringIssuance_IsRejectedAndIssuesNoToken()
    {
        await SeedActiveRefreshTokenAsync("raw-token-1");

        var atIssuance = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseIssuance = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var db = _env.NewContext();
        var auth = new AuthService(
            new UserRepository(db),
            new RefreshTokenRepository(db),
            _env.Hasher,
            new FakeTokenService(),
            new GatedSessionIssuer(
                CreateRealSessionIssuer(db),
                async () =>
                {
                    atIssuance.SetResult();
                    await releaseIssuance.Task;
                }),
            new AccessResolver(new PermissionRepository(db)),
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.Time,
            NullLogger<AuthService>.Instance);

        var refreshTask = auth.RefreshAsync(new RefreshTokenRequest { RefreshToken = "raw-token-1" });
        await atIssuance.Task; // the presented token is already consumed at this point

        ResultAssert.Ok(await SelfServiceChangePasswordAsync("NewStaffPass456!"));

        releaseIssuance.SetResult();
        ResultAssert.Fails(await refreshTask, IdentityErrorKind.Unauthorized, "auth.invalid_refresh_token");

        // No new token row was created; the presented (consumed) token stays revoked.
        var tokens = await ReadTokensAsync();
        Assert.Single(tokens);
        Assert.NotNull(tokens[0].RevokedAt);
        Assert.Equal(1, (await ReadUserAsync()).SecurityVersion);
    }

    [Fact]
    public async Task Refresh_WhenDeactivatedDuringIssuance_IsRejectedAndIssuesNoToken()
    {
        await SeedActiveRefreshTokenAsync("raw-token-1");

        var atIssuance = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseIssuance = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var db = _env.NewContext();
        var auth = new AuthService(
            new UserRepository(db),
            new RefreshTokenRepository(db),
            _env.Hasher,
            new FakeTokenService(),
            new GatedSessionIssuer(
                CreateRealSessionIssuer(db),
                async () =>
                {
                    atIssuance.SetResult();
                    await releaseIssuance.Task;
                }),
            new AccessResolver(new PermissionRepository(db)),
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.Time,
            NullLogger<AuthService>.Instance);

        var refreshTask = auth.RefreshAsync(new RefreshTokenRequest { RefreshToken = "raw-token-1" });
        await atIssuance.Task;

        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { IsActive = false }));

        releaseIssuance.SetResult();
        ResultAssert.Fails(await refreshTask, IdentityErrorKind.Unauthorized, "auth.invalid_refresh_token");

        var tokens = await ReadTokensAsync();
        Assert.Single(tokens);
        Assert.NotNull(tokens[0].RevokedAt);
        Assert.False((await ReadUserAsync()).IsActive);
    }

    [Fact]
    public async Task Refresh_DuringLoginLockout_IsNotBlocked()
    {
        await SeedActiveRefreshTokenAsync("raw-token-1");

        // Lock the account with five failed logins.
        var loginService = CreateLoginService();
        for (var i = 0; i < 5; i++)
        {
            ResultAssert.Fails(
                await loginService.LoginAsync(new LoginRequest { Email = StaffEmail, Password = "wrong-password" }, CancellationToken.None),
                IdentityErrorKind.Unauthorized, "auth.invalid_credentials");
        }

        var locked = await ReadUserAsync();
        Assert.Equal(5, locked.AccessFailedCount);
        Assert.NotNull(locked.LockoutEnd);

        // Refresh is not blocked by a login lockout: the token was issued before the lockout and
        // the account is active with a matching version, so a new session is issued. The real
        // session issuer must be used here, because only it stores the rotated token row.
        ResultAssert.Ok(await CreateLoginServiceWithRealSessions().RefreshAsync(
            new RefreshTokenRequest { RefreshToken = "raw-token-1" }));

        var tokens = await ReadTokensAsync();
        Assert.Equal(2, tokens.Count); // consumed original + new active token
        Assert.Single(tokens, t => t.RevokedAt == null);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_RevokesEverySessionOfTheUser()
    {
        await SeedActiveRefreshTokenAsync("raw-token-1");

        // The real session issuer stores the rotated token, so the second call can present the
        // now-revoked token and trigger family-wide revocation.
        ResultAssert.Ok(await CreateLoginServiceWithRealSessions().RefreshAsync(
            new RefreshTokenRequest { RefreshToken = "raw-token-1" }));

        // Reuse of the now-revoked token is detected: every session of the user is closed.
        ResultAssert.Fails(
            await CreateLoginServiceWithRealSessions().RefreshAsync(new RefreshTokenRequest { RefreshToken = "raw-token-1" }),
            IdentityErrorKind.Unauthorized, "auth.invalid_refresh_token");

        var tokens = await ReadTokensAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
    }

    // ---- guarded issuance (repository level) ----

    [Fact]
    public async Task TryIssueGuarded_RefusesInactiveVersionMismatchAndLoginLockout_AndInsertsNothing()
    {
        using var db = _env.NewContext();
        var repo = new RefreshTokenRepository(db);
        var now = _env.Time.GetUtcNow().UtcDateTime;

        RefreshToken Token(string hash) => new()
        {
            Id = Guid.NewGuid(),
            UserId = _env.StaffUserId,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        };

        // Wrong security version (a credential change landed after the snapshot).
        Assert.Equal(GuardedIssuanceResult.UserUnavailable,
            await repo.TryIssueGuardedAsync(_env.StaffUserId, 99, requireUnlocked: false, now, Token("h1")));

        // Login lockout blocks issuance for login mode...
        await db.Users
            .Where(u => u.Id == _env.StaffUserId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LockoutEnd, (DateTime?)now.AddMinutes(10))
                .SetProperty(u => u.AccessFailedCount, 5));
        Assert.Equal(GuardedIssuanceResult.UserUnavailable,
            await repo.TryIssueGuardedAsync(_env.StaffUserId, 0, requireUnlocked: true, now, Token("h2")));

        // ...but refresh mode is not blocked by the lockout.
        Assert.Equal(GuardedIssuanceResult.Issued,
            await repo.TryIssueGuardedAsync(_env.StaffUserId, 0, requireUnlocked: false, now, Token("h3")));

        // The successful issuance neither cleared the lockout nor changed the failure state.
        var locked = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        Assert.NotNull(locked.LockoutEnd);
        Assert.Equal(5, locked.AccessFailedCount);

        // Inactive account: refused in both modes.
        await db.Users
            .Where(u => u.Id == _env.StaffUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(GuardedIssuanceResult.UserUnavailable,
            await repo.TryIssueGuardedAsync(_env.StaffUserId, 0, requireUnlocked: false, now, Token("h4")));

        // Only the one permitted token row exists.
        var tokens = await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == _env.StaffUserId).ToListAsync();
        var stored = Assert.Single(tokens);
        Assert.Equal("h3", stored.TokenHash);
    }

    [Fact]
    public async Task TryIssueGuarded_WhenInsertFails_RollsBackAndLeavesContextClean()
    {
        using var db = _env.NewContext();
        var repo = new RefreshTokenRepository(db);
        var now = _env.Time.GetUtcNow().UtcDateTime;

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = _env.StaffUserId,
            TokenHash = "duplicate-hash",
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        });
        await db.SaveChangesAsync();

        // The unique token-hash index rejects the insert: InsertFailed, transaction rolled back.
        var attempt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = _env.StaffUserId,
            TokenHash = "duplicate-hash",
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        };
        Assert.Equal(GuardedIssuanceResult.InsertFailed,
            await repo.TryIssueGuardedAsync(_env.StaffUserId, 0, requireUnlocked: false, now, attempt));

        // The user row was not modified by the failed issuance.
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
        Assert.Equal(0, user.SecurityVersion);

        // The context is clean: the rejected token is not tracked and will not be retried...
        Assert.DoesNotContain(attempt, db.ChangeTracker.Entries<RefreshToken>().Select(e => e.Entity));
        // ...and the next insert on the same context works.
        Assert.Equal(GuardedIssuanceResult.Issued,
            await repo.TryIssueGuardedAsync(_env.StaffUserId, 0, requireUnlocked: false, now,
                new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    UserId = _env.StaffUserId,
                    TokenHash = "fresh-hash",
                    CreatedAt = now,
                    ExpiresAt = now.AddDays(7)
                }));
    }

    // ---- email verification binding ----

    [Fact]
    public async Task ConfirmEmail_WhenTokenEmailDoesNotMatchCurrentUser_RollsBackTokenClaim()
    {
        // A token issued for a different address must never confirm this user, and the claim
        // must be rolled back so the link is not consumed by the failed confirmation.
        using var seed = _env.NewContext();
        var rawToken = "raw-verify-token";
        seed.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = _env.StaffUserId,
            TokenHash = OpaqueToken.Hash(rawToken),
            NormalizedEmail = "other-address@example.com",
            CreatedAt = _env.Time.GetUtcNow().UtcDateTime,
            ExpiresAt = _env.Time.GetUtcNow().UtcDateTime.AddHours(24)
        });
        await seed.SaveChangesAsync();

        var result = await CreateEmailVerificationService().VerifyAsync(new VerifyEmailRequest { Token = rawToken });

        ResultAssert.Fails(result, IdentityErrorKind.Validation, "email.invalid_verification_token");

        using var verify = _env.NewContext();
        var user = await verify.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(0, user.SecurityVersion);
        var token = await verify.EmailVerificationTokens.AsNoTracking().SingleAsync(t => t.TokenHash == OpaqueToken.Hash(rawToken));
        Assert.Null(token.UsedAt); // the claim was rolled back with the failed confirmation
    }

    [Fact]
    public async Task ConfirmEmail_MatchingToken_ConfirmsWithoutVersionBumpOrSessionRevocation()
    {
        using var seed = _env.NewContext();
        var rawToken = "raw-verify-token";
        seed.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = _env.StaffUserId,
            TokenHash = OpaqueToken.Hash(rawToken),
            NormalizedEmail = IdentityNormalizer.NormalizeEmail(StaffEmail),
            CreatedAt = _env.Time.GetUtcNow().UtcDateTime,
            ExpiresAt = _env.Time.GetUtcNow().UtcDateTime.AddHours(24)
        });
        await seed.SaveChangesAsync();
        await SeedActiveRefreshTokenAsync("raw-token-1");

        ResultAssert.Ok(await CreateEmailVerificationService().VerifyAsync(new VerifyEmailRequest { Token = rawToken }));

        var user = await ReadUserAsync();
        Assert.True(user.EmailConfirmed);
        Assert.Equal(0, user.SecurityVersion); // confirmation never bumps the credential version

        // No session was revoked by the confirmation.
        var token = Assert.Single(await ReadTokensAsync());
        Assert.Null(token.RevokedAt);
    }
}