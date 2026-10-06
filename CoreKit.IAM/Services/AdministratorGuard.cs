using CoreKit.IAM.Normalization;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Services;

/// <summary>
/// Prevents the one mistake that locks everybody out: removing, deactivating or deleting
/// the last administrator.
/// </summary>
internal static class AdministratorGuard
{
    public static async Task<bool> IsLastActiveAdministratorAsync(IamDbContext db, Guid userId, CancellationToken ct)
    {
        var administrator = IamNormalizer.NormalizeName(IamRoleNames.Administrator);

        var activeAdministratorIds = await db.UserRoles
            .Where(ur => ur.Role.NormalizedName == administrator && ur.Role.IsActive && ur.User.IsActive)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        return activeAdministratorIds.Count == 1 && activeAdministratorIds[0] == userId;
    }
}
