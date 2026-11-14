using CoreKit.Access.Abstractions;
using CoreKit.Access.Catalog;
using CoreKit.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace CoreKit.Identity.Seeding;

/// <summary>
/// Safe to run on every startup. It creates what is missing and never removes anything: the first
/// administrator. Roles and permissions are owned by Access (the catalog); the seeder only ensures the
/// platform Administrator role exists with every catalog permission and makes the first user a platform
/// member holding it.
/// </summary>
public sealed class IdentitySeeder
{
    private static readonly AccessScope PlatformScope = AccessScope.ForPlatform(AccessScope.PlatformId);

    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _time;
    private readonly IdentitySeedOptions _options;
    private readonly IAccessService _access;
    private readonly IAccessMaintenance _maintenance;
    private readonly AccessCatalog _catalog;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        IdentityDbContext db,
        IPasswordHasher hasher,
        TimeProvider time,
        IOptions<IdentitySeedOptions> options,
        IAccessService access,
        IAccessMaintenance maintenance,
        AccessCatalog catalog,
        ILogger<IdentitySeeder> logger)
    {
        _db = db;
        _hasher = hasher;
        _time = time;
        _options = options.Value;
        _access = access;
        _maintenance = maintenance;
        _catalog = catalog;
        _logger = logger;
    }

    /// <summary>Creates the first administrator if none exists yet. Idempotent.</summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // If any platform member already holds the Administrator role, the first admin exists.
        if ((await _access.ListUserIdsWithRoleAsync(PlatformScope, "Administrator", ct)).Count > 0)
            return;

        var email = _options.AdminEmail?.Trim();
        var mobile = _options.AdminMobile?.Trim();
        var password = _options.AdminPassword;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning(
                "Identity has no administrator yet. Set Identity:Seed:AdminEmail and Identity:Seed:AdminPassword to create one.");
            return;
        }

        if (!EmailValidator.IsValid(email))
            throw new InvalidOperationException("Identity:Seed:AdminEmail is not a valid email address.");

        if (string.IsNullOrWhiteSpace(mobile))
            throw new InvalidOperationException("Identity:Seed:AdminMobile is required to create the first administrator.");

        var normalizedMobile = MobileValidator.Normalize(mobile);

        if (normalizedMobile is null)
            throw new InvalidOperationException("Identity:Seed:AdminMobile is not a valid mobile number.");

        var passwordError = PasswordPolicy.Validate(password, email);

        if (passwordError is not null)
            throw new InvalidOperationException($"Identity:Seed:AdminPassword is not acceptable. {passwordError}");

        var normalizedEmail = IdentityNormalizer.NormalizeEmail(email);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            Mobile = mobile,
            NormalizedMobile = normalizedMobile,
            PasswordHash = _hasher.Hash(password),
            DisplayName = _options.AdminDisplayName,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // Ensure the Administrator role holds every catalog permission (including newer modules).
        var adminRoleId = await _maintenance.EnsurePlatformRoleAsync(
            "Administrator", "Full access. Cannot be changed or deleted.", _catalog.Names.ToList(), ct);

        await _maintenance.AddPlatformMemberAsync(user.Id, ct);
        await _maintenance.SetPlatformUserRolesAsync(user.Id, new[] { adminRoleId }, ct);

        _logger.LogInformation("The first Identity administrator {UserId} was created.", user.Id);
    }
}