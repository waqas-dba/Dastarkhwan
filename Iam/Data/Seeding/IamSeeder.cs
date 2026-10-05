using Iam.Core.Constants;
using Iam.Core.Entities;
using Iam.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Iam.Data.Seeding;

/// <summary>
/// Creates the data IAM cannot work without: its own permissions, the Administrator role
/// and two starting client apps. Safe to run any number of times.
/// </summary>
public sealed class IamSeeder
{
    private readonly IamDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<IamSeeder> _logger;

    public IamSeeder(IamDbContext db, TimeProvider time, ILogger<IamSeeder> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();

        await SeedPermissionsAsync(now, ct);
        await SeedAdministratorRoleAsync(now, ct);
        await EnsureClientAsync("web-admin", "Web admin", ClientType.Web, now, ct);
        await EnsureClientAsync("desktop-app", "Desktop app", ClientType.Desktop, now, ct);

        _logger.LogInformation("IAM seed data is in place.");
    }

    private async Task SeedPermissionsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var existing = await _db.Permissions.ToDictionaryAsync(p => p.Code, ct);

        foreach (var definition in IamPermissions.All)
        {
            if (existing.TryGetValue(definition.Code, out var permission))
            {
                permission.UpdateDescription(definition.Description);
                continue;
            }

            var created = Permission.Create(definition.Code, definition.Description, now);

            if (created.IsFailure)
                throw new InvalidOperationException(
                    $"Built-in permission '{definition.Code}' is invalid: {created.Error.Message}");

            _db.Permissions.Add(created.Value);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedAdministratorRoleAsync(DateTimeOffset now, CancellationToken ct)
    {
        var normalizedName = IamRoles.Administrator.ToUpperInvariant();

        var role = await _db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, ct);

        if (role is null)
        {
            var created = Role.Create(IamRoles.Administrator, "Full access to IAM", isSystem: true, now);

            if (created.IsFailure)
                throw new InvalidOperationException($"Built-in role is invalid: {created.Error.Message}");

            role = created.Value;
            _db.Roles.Add(role);
        }

        var iamCodes = IamPermissions.All.Select(d => d.Code).ToList();

        var iamPermissions = await _db.Permissions
            .Where(p => iamCodes.Contains(p.Code))
            .ToListAsync(ct);

        foreach (var permission in iamPermissions)
            role.GrantPermission(permission.Id, now);

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureClientAsync(
        string clientId, string name, ClientType type, DateTimeOffset now, CancellationToken ct)
    {
        if (await _db.ClientApplications.AnyAsync(c => c.ClientId == clientId, ct))
            return;

        var created = ClientApplication.Create(clientId, name, type, now);

        if (created.IsFailure)
            throw new InvalidOperationException(
                $"Built-in client '{clientId}' is invalid: {created.Error.Message}");

        _db.ClientApplications.Add(created.Value);
        await _db.SaveChangesAsync(ct);
    }
}