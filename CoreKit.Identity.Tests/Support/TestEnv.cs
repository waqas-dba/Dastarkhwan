global using CoreKit.Identity.Contracts;

using CoreKit.Identity.Constants;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Repositories;
using CoreKit.Identity.Security;
using CoreKit.Identity.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace CoreKit.Identity.Tests.Support;

/// <summary>
/// One real object graph on an in-memory SQLite database, built like a single request.
/// Repositories are created from one context and read back from another, so nothing is
/// served from a tracker. Users are seeded directly into the database so tests start from a
/// known state. Platform roles and membership are owned by Access, so Identity tests no longer
/// configure platform access — authorization is exercised through the fake
/// <see cref="FakeIdentityAdminAuthorizer"/> instead.
/// </summary>
internal sealed class TestEnv : IDisposable
{
    private readonly string _databasePath;
    private readonly List<SqliteConnection> _connections = new();

    public TestEnv()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"corekit_identity_tests_{Guid.NewGuid():N}.db");

        var seedConnection = new SqliteConnection($"Data Source={_databasePath};Foreign Keys=True");
        seedConnection.Open();
        _connections.Add(seedConnection);

        using (var setup = NewContext())
            setup.Database.EnsureCreated();

        Db = NewContext();
        Time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        Hasher = new PasswordHasher();

        // ---- seed users (no role tables: Access owns roles) ----
        OwnerUserId = Guid.NewGuid();
        var owner = new User
        {
            Id = OwnerUserId,
            Email = "owner@example.com",
            NormalizedEmail = IdentityNormalizer.NormalizeEmail("owner@example.com"),
            Mobile = "+15551000001",
            NormalizedMobile = "+15551000001",
            PasswordHash = Hasher.Hash("OwnerPass123!"),
            DisplayName = "Owner",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = Time.GetUtcNow().UtcDateTime
        };

        StaffUserId = Guid.NewGuid();
        var staff = new User
        {
            Id = StaffUserId,
            Email = "staff@example.com",
            NormalizedEmail = IdentityNormalizer.NormalizeEmail("staff@example.com"),
            Mobile = "+15551000002",
            NormalizedMobile = "+15551000002",
            PasswordHash = Hasher.Hash("StaffPass123!"),
            DisplayName = "Staff Member",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = Time.GetUtcNow().UtcDateTime
        };

        InactiveUserId = Guid.NewGuid();
        var inactive = new User
        {
            Id = InactiveUserId,
            Email = "inactive@example.com",
            NormalizedEmail = IdentityNormalizer.NormalizeEmail("inactive@example.com"),
            Mobile = "+15551000003",
            NormalizedMobile = "+15551000003",
            PasswordHash = Hasher.Hash("InactivePass123!"),
            DisplayName = "Inactive User",
            IsActive = false,
            EmailConfirmed = true,
            CreatedAt = Time.GetUtcNow().UtcDateTime
        };

        Db.Users.AddRange(owner, staff, inactive);
        Db.SaveChanges();

        // ---- services ----
        Authorizer = new FakeIdentityAdminAuthorizer();
        Authorizer.AllowAll();

        var riskGate = new FakeLoginRiskGate();

        UserRepository = new UserRepository(Db);
        RefreshTokenRepository = new RefreshTokenRepository(Db);
        AdministratorGuard = new AdministratorGuard(
            new FakePlatformAdministratorProvider(),
            UserRepository,
            Db);
        CurrentUser = new FakeCurrentUserService();
        UserDeletionGuard = new FakeUserDeletionGuard();
        EventPublisher = new FakeIdentityEventPublisher();
        LockoutOptions = Options.Create(new IdentityLockoutOptions { Threshold = 5, DurationMinutes = 15 });

        UserService = new UserService(
            UserRepository, Db, Authorizer, AdministratorGuard, Hasher, CurrentUser, Time, UserDeletionGuard, EventPublisher);

        var emailOptions = Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false });
        AuthService = new AuthService(
            UserRepository, RefreshTokenRepository, Hasher,
            new FakeTokenService(), new FakeSessionIssuer(),
            CurrentUser, emailOptions, LockoutOptions, Time,
            riskGate,
            NullLogger<AuthService>.Instance);
    }

    public IdentityDbContext Db { get; }

    public FixedTimeProvider Time { get; }

    public PasswordHasher Hasher { get; }

    public FakeCurrentUserService CurrentUser { get; }

    public FakeIdentityAdminAuthorizer Authorizer { get; }

    public IUserRepository UserRepository { get; }

    public IRefreshTokenRepository RefreshTokenRepository { get; }

    public IAdministratorGuard AdministratorGuard { get; }

    public IUserDeletionGuard UserDeletionGuard { get; }

    public IIdentityEventPublisher EventPublisher { get; }

    public IOptions<IdentityLockoutOptions> LockoutOptions { get; }

    public IUserService UserService { get; }

    public IAuthService AuthService { get; }

    public Guid OwnerUserId { get; }

    public Guid StaffUserId { get; }

    public Guid InactiveUserId { get; }

    public IdentityDbContext NewContext()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath};Foreign Keys=True");
        connection.Open();
        _connections.Add(connection);
        return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options);
    }

    public void SignInOwner()
    {
        CurrentUser.UserId = OwnerUserId;
        CurrentUser.Email = "owner@example.com";
    }

    public void SignInStaff()
    {
        CurrentUser.UserId = StaffUserId;
        CurrentUser.Email = "staff@example.com";
    }

    public void SignInWith(Guid userId, string? email = null)
    {
        CurrentUser.UserId = userId;
        CurrentUser.Email = email;
    }

    public void SignOut()
    {
        CurrentUser.UserId = null;
        CurrentUser.Email = null;
    }

    public void Dispose()
    {
        Db.Dispose();
        foreach (var c in _connections) c.Dispose();
        try { File.Delete(_databasePath); } catch { /* best-effort cleanup */ }
    }
}

/// <summary>Minimal fake ITokenService for tests.</summary>
internal sealed class FakeTokenService : ITokenService
{
    public AccessTokenResult CreateAccessToken(User user, IEnumerable<Claim>? extraClaims = null)
        => new("fake-access-token", DateTime.UtcNow.AddMinutes(5));

    public GeneratedRefreshToken CreateRefreshToken()
        => new("raw-token", "hash-token", DateTime.UtcNow.AddDays(7));

    public string HashRefreshToken(string rawToken)
        => "hash-" + rawToken;
}

/// <summary>Minimal fake IPlatformAdministratorProvider for tests.</summary>
internal sealed class FakePlatformAdministratorProvider : IPlatformAdministratorProvider
{
    public IReadOnlyList<Guid> AdminIds { get; set; } = Array.Empty<Guid>();

    public Task<IReadOnlyList<Guid>> GetPlatformAdministratorUserIdsAsync(CancellationToken ct = default)
        => Task.FromResult(AdminIds);
}

/// <summary>Minimal fake ISessionIssuer for tests: always succeeds, like the unguarded baseline.</summary>
internal sealed class FakeSessionIssuer : ISessionIssuer
{
    public Task<IdentityResult<LoginResponse>> IssueForLoginAsync(
        User user, int expectedSecurityVersion, CancellationToken ct = default)
        => Task.FromResult(IdentityResult.Success(Build(user)));

    public Task<IdentityResult<LoginResponse>> IssueForRefreshAsync(
        User user, int expectedSecurityVersion, Guid familyId, CancellationToken ct = default)
        => Task.FromResult(IdentityResult.Success(Build(user)));

    public Task<IdentityResult<LoginResponse>> RotateAsync(
        User user, int expectedSecurityVersion, Guid oldTokenId, Guid familyId, CancellationToken ct = default)
        => Task.FromResult(IdentityResult.Success(Build(user)));

    private static LoginResponse Build(User user) => new()
    {
        AccessToken = "fake-access-token",
        AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(5),
        RefreshToken = "fake-refresh-token",
        RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7),
        User = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        }
    };
}

/// <summary>Minimal fake IUserDeletionGuard for tests: always allows deletion.</summary>
internal sealed class FakeUserDeletionGuard : IUserDeletionGuard
{
    public Task<bool> CanDeleteUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(true);
}

/// <summary>Minimal fake IIdentityEventPublisher for tests: no-op.</summary>
internal sealed class FakeIdentityEventPublisher : IIdentityEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIdentityIntegrationEvent
        => Task.CompletedTask;
}

/// <summary>Minimal fake ILoginRiskGate for tests: never requires captcha.</summary>
internal sealed class FakeLoginRiskGate : ILoginRiskGate
{
    public Task<Common.IdentityResult> CheckAsync(string normalizedEmail, string? captchaToken, CancellationToken ct = default)
        => Task.FromResult(Common.IdentityResult.Success());

    public void RecordFailure(string normalizedEmail) { }

    public void RecordSuccess(string normalizedEmail) { }
}
