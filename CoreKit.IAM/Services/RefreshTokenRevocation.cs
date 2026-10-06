using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Services;

internal static class RefreshTokenRevocation
{
    /// <summary>
    /// Ends every session of a user in one statement. Used after a password change, a deactivation
    /// and when a stolen token is detected.
    /// </summary>
    public static Task<int> RevokeAllForUserAsync(IamDbContext db, Guid userId, DateTime now, CancellationToken ct)
        => db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now), ct);
}