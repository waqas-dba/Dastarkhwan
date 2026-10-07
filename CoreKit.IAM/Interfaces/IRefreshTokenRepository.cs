using CoreKit.IAM.Entities;

namespace CoreKit.IAM.Interfaces;

public interface IRefreshTokenRepository
{
    /// <summary>Read-only, with the user, the user's roles and each role.</summary>
    Task<RefreshToken?> FindByHashWithUserAsync(string tokenHash, CancellationToken ct = default);

    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>
    /// Atomic. Of two requests using the same token at the same moment, only one gets true.
    /// </summary>
    Task<bool> TryRevokeAsync(Guid tokenId, DateTime now, CancellationToken ct = default);

    /// <summary>Revoking an unknown or already revoked token is not an error.</summary>
    Task RevokeByHashAsync(string tokenHash, DateTime now, CancellationToken ct = default);

    /// <summary>Ends every session of a user in one statement.</summary>
    Task<int> RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken ct = default);
}