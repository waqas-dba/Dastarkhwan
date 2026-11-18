using CoreKit.Identity.Entities;
using CoreKit.Identity.Normalization;
using CoreKit.Identity.Persistence;
using CoreKit.Identity.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Identity.Seeding;

/// <summary>
/// Safe to run on every startup. It creates what is missing and never removes anything: the first
/// administrator. Platform roles and membership are owned by Access; the seeder only ensures the
/// first Identity user exists with administrator privileges.
/// </summary>
public sealed class IdentitySeeder
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _time;
    private readonly IdentitySeedOptions _options;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        IdentityDbContext db,
        IPasswordHasher hasher,
        TimeProvider time,
        IOptions<IdentitySeedOptions> options,
        ILogger<IdentitySeeder> logger)
    {
        _db = db;
        _hasher = hasher;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Creates the first administrator if none exists yet. Idempotent.</summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
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

        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct))
        {
            _logger.LogInformation("The first Identity administrator already exists.");
            return;
        }

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

        _logger.LogInformation("The first Identity administrator {UserId} was created.", user.Id);
    }
}
