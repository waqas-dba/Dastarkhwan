using CoreKit.Identity.Constants;
using System.Security;

namespace CoreKit.Tenant.Integration;

public static class TenantPermissionSeeds
{
    /// <summary>Pass to Identity's seeder: await app.Services.SeedIdentityAsync(TenantPermissionSeeds.All).</summary>
    public static IReadOnlyList<PermissionSeed> All { get; } = TenantPermissionNames.All
        .Select(p => new PermissionSeed(p.Name, p.Description))
        .ToList();
}