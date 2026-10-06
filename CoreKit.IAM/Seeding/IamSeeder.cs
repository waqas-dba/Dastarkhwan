using Microsoft.Extensions.Logging;

namespace CoreKit.IAM.Seeding;

/// <summary>
/// Safe to run on every startup. It creates what is missing and never removes anything:
/// permissions, the default roles, and the first admin user.
/// </summary>
public sealed class IamSeeder
{
    // Roles besides Administrator. Created once with these permissions; an administrator owns them afterwards.
    private static readonly (string Name, string Description, string[] Permissions)[] DefaultRoles =
    {
        (IamRoleNames.Manager, "Manages users and can read roles.",
            new[]
            {
                IamPermissionNames.UsersRead,
                IamPermissionNames.UsersCreate,
                IamPermissionNames.UsersUpdate,
                IamPermissionNames.RolesRead,
                IamPermissionNames.PermissionsRead
            }),

        (IamRoleNames.Staff, "Basic read access.",
            new[] { IamPermissionNames.UsersRead })
    };

    private readonly IamDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _time;
    private readonly IamSeedOptions _options;
    private readonly ILogger<IamSeeder> _logger;

    public IamSeeder(
        IamDbContext db,
        IPasswordHasher hasher,
        TimeProvider time,
        IOptions<IamSeedOptions> options,
        ILogger<IamSeeder> logger)
    {
        _db = db;
        _hasher = hasher;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    /// <param name="modulePermissions">Permissions that other modules define for themselves.</param>
    public async Task SeedAsync(IEnumerable<PermissionSeed>? modulePermissions = null, CancellationToken ct = default)
    {
        var permissions = await SeedPermissionsAsync(modulePermissions, ct);
        var admin = await SeedAdministratorRoleAsync(permissions, ct);
        await SeedDefaultRolesAsync(permissions, ct);

        await _db.SaveChangesAsync(ct);

        await SeedFirstAdministratorAsync(admin, ct);
    }

    private async Task<List<Permission>> SeedPermissionsAsync(
        IEnumerable<PermissionSeed>? modulePermissions, CancellationToken ct)
    {
        var wanted = IamPermissionNames.All
            .Concat(modulePermissions ?? Enumerable.Empty<PermissionSeed>())
            .Select(p => new PermissionSeed(p.Name.Trim().ToLowerInvariant(), p.Description))
            .DistinctBy(p => p.Name)
            .ToList();

        var existing = await _db.Permissions.ToListAsync(ct);

        foreach (var seed in wanted)
        {
            var permission = existing.FirstOrDefault(p => p.Name == seed.Name);

            if (permission is null)
            {
                permission = new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = seed.Name,
                    Description = seed.Description,
                    IsActive = true
                };

                _db.Permissions.Add(permission);
                existing.Add(permission);
            }
            else if (permission.Description != seed.Description)
            {
                permission.Description = seed.Description;
            }
        }

        return existing;
    }

    private async Task<Role> SeedAdministratorRoleAsync(List<Permission> permissions, CancellationToken ct)
    {
        var normalized = IamNormalizer.NormalizeName(IamRoleNames.Administrator);

        var admin = await _db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.NormalizedName == normalized, ct);

        if (admin is null)
        {
            admin = new Role
            {
                Id = Guid.NewGuid(),
                Name = IamRoleNames.Administrator,
                NormalizedName = normalized,
                Description = "Full access. Cannot be changed or deleted.",
                IsActive = true
            };

            _db.Roles.Add(admin);
        }
        else if (!admin.IsActive)
        {
            admin.IsActive = true;
        }

        // Administrator always holds every permission, including ones added by newer modules.
        var linked = admin.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

        foreach (var permission in permissions.Where(p => !linked.Contains(p.Id)))
            admin.RolePermissions.Add(new RolePermission { RoleId = admin.Id, PermissionId = permission.Id });

        return admin;
    }

    private async Task SeedDefaultRolesAsync(List<Permission> permissions, CancellationToken ct)
    {
        foreach (var (name, description, permissionNames) in DefaultRoles)
        {
            var normalized = IamNormalizer.NormalizeName(name);

            // Already there? Leave it alone: an administrator may have changed it on purpose.
            if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalized, ct))
                continue;

            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name = name,
                NormalizedName = normalized,
                Description = description,
                IsActive = true
            };

            foreach (var permissionName in permissionNames)
            {
                var permission = permissions.FirstOrDefault(p => p.Name == permissionName);

                if (permission is not null)
                    role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }

            _db.Roles.Add(role);
        }
    }

    private async Task SeedFirstAdministratorAsync(Role admin, CancellationToken ct)
    {
        if (await _db.UserRoles.AnyAsync(ur => ur.RoleId == admin.Id, ct))
            return;

        var email = _options.AdminEmail?.Trim();
        var password = _options.AdminPassword;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning(
                "IAM has no administrator yet. Set Iam:Seed:AdminEmail and Iam:Seed:AdminPassword to create one.");
            return;
        }

        if (!EmailValidator.IsValid(email))
            throw new InvalidOperationException("Iam:Seed:AdminEmail is not a valid email address.");

        var passwordError = PasswordPolicy.Validate(password, email);

        if (passwordError is not null)
            throw new InvalidOperationException($"Iam:Seed:AdminPassword is not acceptable. {passwordError}");

        var normalizedEmail = IamNormalizer.NormalizeEmail(email);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                NormalizedEmail = normalizedEmail,
                PasswordHash = _hasher.Hash(password),
                DisplayName = _options.AdminDisplayName,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = _time.GetUtcNow().UtcDateTime
            };

            _db.Users.Add(user);
        }

        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = admin.Id });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("The first IAM administrator {UserId} was created.", user.Id);
    }
}