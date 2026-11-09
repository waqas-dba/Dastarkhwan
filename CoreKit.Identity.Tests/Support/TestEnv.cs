using CoreKit.Identity.Repositories;
using CoreKit.Identity.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Threading;

namespace CoreKit.Identity.Tests.Support;

/// <summary>
/// One real object graph on an in-memory SQLite database, built like a single request.
/// Repositories are created from one context and read back from another, so nothing is
/// served from a tracker. Permissions, roles and users are seeded directly into the
/// database so tests start from a known state.
/// </summary>
internal sealed class TestEnv : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<IdentityDbContext> _dbOptions;

    public TestEnv()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var setup = NewContext())
            setup.Database.EnsureCreated();

        Db = NewContext();
        Time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        Hasher = new PasswordHasher();

        // ---- seed permissions ----
        var permissions = new List<Permission>();
        foreach (var seed in IdentityPermissionNames.All)
        {
            permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Name = seed.Name,
                Description = seed.Description,
                IsActive = true
            });
        }
        Db.Permissions.AddRange(permissions);
        Db.SaveChanges();

        // ---- seed roles ----
        var adminPerms = permissions.ToDictionary(p => p.Name);
        var adminRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = IdentityRoleNames.Administrator,
            NormalizedName = IdentityNormalizer.NormalizeName(IdentityRoleNames.Administrator),
            Description = "Full access. Cannot be changed or deleted.",
            IsActive = true
        };
        foreach (var p in permissions)
            adminRole.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = p.Id });

        var managerPermIds = new[]
        {
            IdentityPermissionNames.UsersRead,
            IdentityPermissionNames.UsersCreate,
            IdentityPermissionNames.UsersUpdate,
            IdentityPermissionNames.RolesRead,
            IdentityPermissionNames.PermissionsRead
        };
        var managerRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = IdentityRoleNames.Manager,
            NormalizedName = IdentityNormalizer.NormalizeName(IdentityRoleNames.Manager),
            Description = "Manages users and can read roles.",
            IsActive = true
        };
        foreach (var name in managerPermIds)
        {
            var p = adminPerms[name];
            managerRole.RolePermissions.Add(new RolePermission { RoleId = managerRole.Id, PermissionId = p.Id });
        }

        var staffRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = IdentityRoleNames.Staff,
            NormalizedName = IdentityNormalizer.NormalizeName(IdentityRoleNames.Staff),
            Description = "Basic read access.",
            IsActive = true
        };
        staffRole.RolePermissions.Add(new RolePermission
            { RoleId = staffRole.Id, PermissionId = adminPerms[IdentityPermissionNames.UsersRead].Id });

        Db.Roles.AddRange(adminRole, managerRole, staffRole);
        Db.SaveChanges();

        AdministratorRoleId = adminRole.Id;
        ManagerRoleId = managerRole.Id;
        StaffRoleId = staffRole.Id;

        UserPermissionId = adminPerms[IdentityPermissionNames.UsersRead].Id;
        UserCreatePermissionId = adminPerms[IdentityPermissionNames.UsersCreate].Id;
        UserUpdatePermissionId = adminPerms[IdentityPermissionNames.UsersUpdate].Id;

        // ---- seed users ----
        OwnerUserId = Guid.NewGuid();
        var owner = new User
        {
            Id = OwnerUserId,
            Email = "owner@example.com",
            NormalizedEmail = IdentityNormalizer.NormalizeEmail("owner@example.com"),
            PasswordHash = Hasher.Hash("OwnerPass123!"),
            DisplayName = "Owner",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = Time.GetUtcNow().UtcDateTime
        };
        owner.UserRoles.Add(new UserRole { UserId = OwnerUserId, RoleId = AdministratorRoleId });

        StaffUserId = Guid.NewGuid();
        var staff = new User
        {
            Id = StaffUserId,
            Email = "staff@example.com",
            NormalizedEmail = IdentityNormalizer.NormalizeEmail("staff@example.com"),
            PasswordHash = Hasher.Hash("StaffPass123!"),
            DisplayName = "Staff Member",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = Time.GetUtcNow().UtcDateTime
        };
        staff.UserRoles.Add(new UserRole { UserId = StaffUserId, RoleId = StaffRoleId });

        InactiveUserId = Guid.NewGuid();
        var inactive = new User
        {
            Id = InactiveUserId,
            Email = "inactive@example.com",
            NormalizedEmail = IdentityNormalizer.NormalizeEmail("inactive@example.com"),
            PasswordHash = Hasher.Hash("InactivePass123!"),
            DisplayName = "Inactive User",
            IsActive = false,
            EmailConfirmed = true,
            CreatedAt = Time.GetUtcNow().UtcDateTime
        };
        inactive.UserRoles.Add(new UserRole { UserId = InactiveUserId, RoleId = StaffRoleId });

        Db.Users.AddRange(owner, staff, inactive);
        Db.SaveChanges();

        // ---- services ----
        UserRepository = new UserRepository(Db);
        RoleRepository = new RoleRepository(Db);
        PermissionRepository = new PermissionRepository(Db);
        RefreshTokenRepository = new RefreshTokenRepository(Db);
        AdministratorGuard = new AdministratorGuard(UserRepository);
        AccessResolver = new AccessResolver(PermissionRepository);
        CurrentUser = new FakeCurrentUserService();

        UserService = new UserService(
            UserRepository, RoleRepository, RefreshTokenRepository,
            AccessResolver, AdministratorGuard, Hasher, CurrentUser, Time);

        RoleService = new RoleService(RoleRepository, PermissionRepository, CurrentUser);

        var emailOptions = Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false });
        AuthService = new AuthService(
            UserRepository, RefreshTokenRepository, Hasher,
            new FakeTokenService(), new FakeSessionIssuer(),
            AccessResolver, CurrentUser, emailOptions, Time,
            NullLogger<AuthService>.Instance);
    }

    public IdentityDbContext Db { get; }

    public FixedTimeProvider Time { get; }

    public PasswordHasher Hasher { get; }

    public FakeCurrentUserService CurrentUser { get; }

    public IUserRepository UserRepository { get; }

    public IRoleRepository RoleRepository { get; }

    public IPermissionRepository PermissionRepository { get; }

    public IRefreshTokenRepository RefreshTokenRepository { get; }

    public IAccessResolver AccessResolver { get; }

    public IAdministratorGuard AdministratorGuard { get; }

    public IUserService UserService { get; }

    public IRoleService RoleService { get; }

    public IAuthService AuthService { get; }

    public Guid OwnerUserId { get; }

    public Guid StaffUserId { get; }

    public Guid InactiveUserId { get; }

    public Guid AdministratorRoleId { get; }

    public Guid ManagerRoleId { get; }

    public Guid StaffRoleId { get; }

    public Guid UserPermissionId { get; }

    public Guid UserCreatePermissionId { get; }

    public Guid UserUpdatePermissionId { get; }

    public IdentityDbContext NewContext() => new(_dbOptions);

    public void SignInOwner()
    {
        CurrentUser.UserId = OwnerUserId;
        CurrentUser.Email = "owner@example.com";
        CurrentUser.Roles = new List<string> { IdentityRoleNames.Administrator };
        CurrentUser.Permissions = IdentityPermissionNames.All.Select(p => p.Name).ToList();
    }

    public void SignInStaff()
    {
        CurrentUser.UserId = StaffUserId;
        CurrentUser.Email = "staff@example.com";
        CurrentUser.Roles = new List<string> { IdentityRoleNames.Staff };
        CurrentUser.Permissions = new List<string> { IdentityPermissionNames.UsersRead };
    }

    public void SignOut()
    {
        CurrentUser.UserId = null;
        CurrentUser.Email = null;
        CurrentUser.Roles = new List<string>();
        CurrentUser.Permissions = new List<string>();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

/// <summary>Minimal fake ITokenService for tests.</summary>
internal sealed class FakeTokenService : ITokenService
{
    public AccessTokenResult CreateAccessToken(
        User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions,
        IEnumerable<Claim>? extraClaims = null)
        => new("fake-access-token", DateTime.UtcNow.AddMinutes(5));

    public GeneratedRefreshToken CreateRefreshToken()
        => new("raw-token", "hash-token", DateTime.UtcNow.AddDays(7));

    public string HashRefreshToken(string rawToken)
        => "hash-" + rawToken;
}

/// <summary>Minimal fake ISessionIssuer for tests.</summary>
internal sealed class FakeSessionIssuer : ISessionIssuer
{
    public Task<LoginResponse> IssueAsync(User user, CancellationToken ct = default)
        => Task.FromResult(new LoginResponse
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
        });
}
