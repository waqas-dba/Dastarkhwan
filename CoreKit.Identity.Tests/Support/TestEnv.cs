using CoreKit.Access.Abstractions;
using CoreKit.Access.Models;
using CoreKit.Identity.Constants;
using CoreKit.Identity.Repositories;
using CoreKit.Identity.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;

namespace CoreKit.Identity.Tests.Support;

/// <summary>
/// One real object graph on an in-memory SQLite database, built like a single request.
/// Repositories are created from one context and read back from another, so nothing is
/// served from a tracker. Users are seeded directly into the database so tests start from a
/// known state. Roles and permissions are owned by Access, not Identity, so the platform access
/// of each user is configured on the in-memory <see cref="FakeAccessState"/> instead of in the
/// Identity schema.
/// </summary>
internal sealed class TestEnv : IDisposable
{
    public const string StaffRoleName = "Staff";

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

        AdministratorRoleId = Guid.NewGuid();
        StaffRoleId = Guid.NewGuid();

        // ---- Access platform state (roles + per-user access) ----
        AccessState = new FakeAccessState();
        AccessState.PlatformRoles.Add(new AccessRoleDto
        {
            Id = AdministratorRoleId,
            Name = IdentityRoleNames.Administrator,
            Description = "Full access."
        });
        AccessState.PlatformRoles.Add(new AccessRoleDto
        {
            Id = StaffRoleId,
            Name = StaffRoleName,
            Description = "Basic read access."
        });

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

        // Owner is an administrator (platform member holding the Administrator role).
        AccessState.SetAccess(OwnerUserId, isMember: true, isOwner: false,
            IdentityAccessPermissionNames.All.Select(p => p.Name), new[] { IdentityRoleNames.Administrator });

        // Staff holds users.read and the Staff role.
        AccessState.SetAccess(StaffUserId, isMember: true, isOwner: false,
            new[] { IdentityAccessPermissionNames.UsersRead }, new[] { StaffRoleName });

        // The inactive user has the same platform access as staff (only their Identity account is off).
        AccessState.SetAccess(InactiveUserId, isMember: true, isOwner: false,
            new[] { IdentityAccessPermissionNames.UsersRead }, new[] { StaffRoleName });

        // ---- Access fakes (backed by the state above) ----
        PermissionChecker = new FakePermissionChecker(AccessState);
        AccessService = new FakeAccessService(AccessState);
        Maintenance = new FakeAccessMaintenance(AccessState);

        // ---- services ----
        UserRepository = new UserRepository(Db);
        RefreshTokenRepository = new RefreshTokenRepository(Db);
        AdministratorGuard = new AdministratorGuard(AccessService);
        CurrentUser = new FakeCurrentUserService();

        UserService = new UserService(
            UserRepository, PermissionChecker, AccessService, Maintenance,
            AdministratorGuard, Hasher, CurrentUser, Time);

        var emailOptions = Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false });
        AuthService = new AuthService(
            UserRepository, RefreshTokenRepository, Hasher,
            new FakeTokenService(), new FakeSessionIssuer(),
            AccessService, CurrentUser, emailOptions, Time,
            NullLogger<AuthService>.Instance);
    }

    public IdentityDbContext Db { get; }

    public FixedTimeProvider Time { get; }

    public PasswordHasher Hasher { get; }

    public FakeCurrentUserService CurrentUser { get; }

    public FakeAccessState AccessState { get; }

    public CoreKit.Access.Abstractions.IPermissionChecker PermissionChecker { get; }

    public IAccessService AccessService { get; }

    public IAccessMaintenance Maintenance { get; }

    public IUserRepository UserRepository { get; }

    public IRefreshTokenRepository RefreshTokenRepository { get; }

    public IAdministratorGuard AdministratorGuard { get; }

    public IUserService UserService { get; }

    public IAuthService AuthService { get; }

    public Guid OwnerUserId { get; }

    public Guid StaffUserId { get; }

    public Guid InactiveUserId { get; }

    public Guid AdministratorRoleId { get; }

    public Guid StaffRoleId { get; }

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
        AccessState.CurrentUserId = OwnerUserId;
    }

    public void SignInStaff()
    {
        CurrentUser.UserId = StaffUserId;
        CurrentUser.Email = "staff@example.com";
        AccessState.CurrentUserId = StaffUserId;
    }

    /// <summary>
    /// Signs in a seeded user with an exact permission set. Tests use this when the point is a
    /// caller who holds one permission (for example create) but not a neighbouring one (read).
    /// Membership and roles stay as configured for that user; only the platform permissions change.
    /// </summary>
    public void SignInWith(Guid userId, params string[] permissions)
    {
        CurrentUser.UserId = userId;
        CurrentUser.Email = null;
        AccessState.CurrentUserId = userId;

        var current = AccessState.Get(userId);
        AccessState.SetAccess(userId, current.IsMember, current.IsOwner, permissions, current.Roles);
    }

    public void SignOut()
    {
        CurrentUser.UserId = null;
        CurrentUser.Email = null;
        AccessState.CurrentUserId = null;
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

/// <summary>Minimal fake ISessionIssuer for tests: always succeeds, like the unguarded baseline.</summary>
internal sealed class FakeSessionIssuer : ISessionIssuer
{
    public Task<IdentityResult<LoginResponse>> IssueForLoginAsync(
        User user, int expectedSecurityVersion, CancellationToken ct = default)
        => Task.FromResult(IdentityResult.Success(Build(user)));

    public Task<IdentityResult<LoginResponse>> IssueForRefreshAsync(
        User user, int expectedSecurityVersion, CancellationToken ct = default)
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