using CoreKit.Access.Contracts;
using CoreKit.Identity.Common;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Repositories;
using CoreKit.Identity.Security;
using CoreKit.Identity.Settings;
using CoreKit.Identity.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoreKit.Identity.Tests;

/// <summary>
/// DK-IDENTITY-LOCKOUT-FIX-04 regression coverage. What this protects: the SecurityVersion
/// credential guard and guarded session issuance — a stale password (after a concurrent
/// change, administrator reset or deactivation) must never create a session, credential
/// updates must be version-conditional and atomic with session revocation, rehash must never
/// overwrite a newer password, and email must be immutable after creation.
/// Every race is forced with deterministic gates (task completion sources), never sleeps, and
/// every assertion checks persisted database state, not just return values.
/// Authorization is exercised through the host's IIdentityAdminAuthorizer port (faked in
/// TestEnv) rather than Access permissions.
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
            currentUser ?? new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.LockoutOptions,
            _env.Time,
            new FakeLoginRiskGate(),
            NullLogger<AuthService>.Instance);
    }

    private UserService CreateAdminService(IUserRepository? userRepository = null)
    {
        var db = _env.NewContext();
        return new(
            userRepository ?? new UserRepository(db),
            db,
            _env.Authorizer,
            _env.AdministratorGuard,
            _env.Hasher,
            OwnerCurrentUser(),
            _env.Time,
            _env.UserDeletionGuard,
            _env.EventPublisher);
    }

    private FakeCurrentUserService OwnerCurrentUser()
        => new()
        {
            UserId = _env.OwnerUserId,
            Email = "owner@example.com"
        };

    private Task<IdentityResult> SelfServiceChangePasswordAsync(string newPassword)
    {
        var auth = CreateLoginService(currentUser: new FakeCurrentUserService
        {
            UserId = _env.StaffUserId,
            Email = StaffEmail
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
            new FakeTokenService(),
            new RefreshTokenRepository(db),
            _env.Time,
            Enumerable.Empty<IAccessTokenClaimsContributor>());

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
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.LockoutOptions,
            _env.Time,
            new FakeLoginRiskGate(),
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
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
        Assert.Equal(PasswordCheckResult.Failed, _env.Hasher.Verify(user.PasswordHash, StaffPassword));
        Assert.Equal(PasswordCheckResult.Success, _env.Hasher.Verify(user.PasswordHash, "NewStaffPass456!"));
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
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.LockoutOptions,
            _env.Time,
            new FakeLoginRiskGate(),
            NullLogger<AuthService>.Instance);

        var loginTask = auth.LoginAsync(new LoginRequest { Email = StaffEmail, Password = StaffPassword }, CancellationToken.None);
        await atIssuance.Task;

        ResultAssert.Ok(await SelfServiceChangePasswordAsync("NewStaffPass456!"));

        releaseIssuance.SetResult();
        ResultAssert.Fails(await loginTask, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

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
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.LockoutOptions,
            _env.Time,
            new FakeLoginRiskGate(),
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

        ResultAssert.Fails(
            await CreateLoginService().LoginAsync(new LoginRequest { Email = StaffEmail, Password = StaffPassword }, CancellationToken.None),
            IdentityErrorKind.Unauthorized, "auth.invalid_credentials");
        ResultAssert.Ok(
            await CreateLoginService().LoginAsync(new LoginRequest { Email = StaffEmail, Password = "NewStaffPass456!" }, CancellationToken.None));
    }

    [Fact]
    public async Task AdminReset_TwoConcurrentResets_LoadedBeforeEitherSaves_OnlyOneApplies()
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

        var first = CreateAdminService(gatedRepo).UpdateAsync(
            _env.StaffUserId, new UpdateUserRequest { Password = "FirstResetPass123!" });
        await gate.Task;

        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { Password = "SecondResetPass456!" }));

        release.SetResult();
        ResultAssert.Fails(await first, IdentityErrorKind.Conflict, "conflict");

        var user = await ReadUserAsync();
        Assert.Equal(1, user.SecurityVersion);
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
            beforeDeactivate: async () =>
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

        Assert.Equal(1, (await ReadUserAsync()).SecurityVersion);
    }

    [Fact]
    public async Task ChangePassword_WhenAccountChangedConcurrently_FailsWithoutApplyingAnything()
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

        var changeTask = CreateLoginService(userRepository: gatedRepo, currentUser: new FakeCurrentUserService
        {
            UserId = _env.StaffUserId,
            Email = StaffEmail
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

        Assert.Equal(CredentialUpdateResult.Updated, await repo.TryChangePasswordAsync(
            _env.StaffUserId, originalHash, 0, "hash:newer-password", now,
            bumpSecurityVersion: true, revokeSessions: true));

        Assert.False(await repo.TryRehashPasswordAsync(_env.StaffUserId, originalHash, 0, "hash:stale-rehash", now));

        var afterChange = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        Assert.Equal("hash:newer-password", afterChange.PasswordHash);
        Assert.Equal(1, afterChange.SecurityVersion);

        Assert.True(await repo.TryRehashPasswordAsync(_env.StaffUserId, "hash:newer-password", 1, "hash:rehash-of-new", now));
        var afterRehash = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        Assert.Equal("hash:rehash-of-new", afterRehash.PasswordHash);
        Assert.Equal(1, afterRehash.SecurityVersion);
    }

    [Fact]
    public async Task Login_WithRehashNeeded_WhenAdminResetLandsDuringRehash_DoesNotIssueSession()
    {
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
        var gatedSessions = new GatedSessionIssuer(
            CreateRealSessionIssuer(db),
            async () => { await Task.Yield(); });
        var auth = new AuthService(
            new UserRepository(db),
            new RefreshTokenRepository(db),
            _env.Hasher,
            new FakeTokenService(),
            gatedSessions,
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.LockoutOptions,
            _env.Time,
            new FakeLoginRiskGate(),
            NullLogger<AuthService>.Instance);

        var loginTask = auth.LoginAsync(new LoginRequest { Email = StaffEmail, Password = "old" }, CancellationToken.None);
        await atRehash.Task;

        ResultAssert.Ok(await AdminUpdateAsync(new UpdateUserRequest { Password = "AdminReset789!" }));

        releaseRehash.SetResult();
        ResultAssert.Fails(await loginTask, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

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
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.LockoutOptions,
            _env.Time,
            new FakeLoginRiskGate(),
            NullLogger<AuthService>.Instance);

        var refreshTask = auth.RefreshAsync(new RefreshTokenRequest { RefreshToken = "raw-token-1" });
        await atIssuance.Task;

        ResultAssert.Ok(await SelfServiceChangePasswordAsync("NewStaffPass456!"));

        releaseIssuance.SetResult();
        ResultAssert.Fails(await refreshTask, IdentityErrorKind.Unauthorized, "auth.invalid_refresh_token");

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
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            _env.LockoutOptions,
            _env.Time,
            new FakeLoginRiskGate(),
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

        var loginService = CreateLoginService();
        for (var i = 0; i < 4; i++)
        {
            ResultAssert.Fails(
                await loginService.LoginAsync(new LoginRequest { Email = StaffEmail, Password = "wrong-password" }, CancellationToken.None),
                IdentityErrorKind.Unauthorized, "auth.invalid_credentials");
        }

        // 5th failed attempt locks the account and returns AccountLocked
        ResultAssert.Fails(
            await loginService.LoginAsync(new LoginRequest { Email = StaffEmail, Password = "wrong-password" }, CancellationToken.None),
            IdentityErrorKind.Forbidden, "auth.account_locked");

        var locked = await ReadUserAsync();
        Assert.Equal(5, locked.AccessFailedCount);
        Assert.NotNull(locked.LockoutEnd);

        ResultAssert.Ok(await CreateLoginServiceWithRealSessions().RefreshAsync(
            new RefreshTokenRequest { RefreshToken = "raw-token-1" }));

        var tokens = await ReadTokensAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, t => t.RevokedAt == null);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_RevokesEverySessionOfTheUser()
    {
        await SeedActiveRefreshTokenAsync("raw-token-1");

        ResultAssert.Ok(await CreateLoginServiceWithRealSessions().RefreshAsync(
            new RefreshTokenRequest { RefreshToken = "raw-token-1" }));

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

        Assert.Equal(GuardedIssuanceResult.UserUnavailable,
            await repo.TryIssueGuardedAsync(_env.StaffUserId, 99, requireUnlocked: false, now, Token("h1")));

        await db.Users
            .Where(u => u.Id == _env.StaffUserId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LockoutEnd, (DateTime?)now.AddMinutes(10))
                .SetProperty(u => u.AccessFailedCount, 5));
        Assert.Equal(GuardedIssuanceResult.UserUnavailable,
            await repo.TryIssueGuardedAsync(_env.StaffUserId, 0, requireUnlocked: true, now, Token("h2")));

        Assert.Equal(GuardedIssuanceResult.Issued,
            await repo.TryIssueGuardedAsync(_env.StaffUserId, 0, requireUnlocked: false, now, Token("h3")));

        var locked = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        Assert.NotNull(locked.LockoutEnd);
        Assert.Equal(5, locked.AccessFailedCount);

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

        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == _env.StaffUserId);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
        Assert.Equal(0, user.SecurityVersion);

        Assert.DoesNotContain(attempt, db.ChangeTracker.Entries<RefreshToken>().Select(e => e.Entity));
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
        using var seed = _env.NewContext();
        var rawToken = "raw-verify-token";

        await seed.Users
            .Where(u => u.Id == _env.StaffUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailConfirmed, false));

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
        Assert.Null(token.UsedAt);
    }

    [Fact]
    public async Task ConfirmEmail_MatchingToken_ConfirmsWithoutVersionBumpOrSessionRevocation()
    {
        using var seed = _env.NewContext();
        var rawToken = "raw-verify-token";

        await seed.Users
            .Where(u => u.Id == _env.StaffUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailConfirmed, false));

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
        Assert.Equal(0, user.SecurityVersion);

        var token = Assert.Single(await ReadTokensAsync());
        Assert.Null(token.RevokedAt);
    }
}
