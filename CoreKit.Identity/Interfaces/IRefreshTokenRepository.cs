using CoreKit.Identity.Entities;

namespace CoreKit.Identity.Interfaces;

public interface IRefreshTokenRepository
{
    /// <summary>Read-only, with the user, the user's roles and each role.</summary>
    Task<RefreshToken?> FindByHashWithUserAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// The single permitted refresh-token insertion route. One transaction on the shared
    /// scoped IdentityDbContext: takes a row lock on the user, re-checks that the account is
    /// active and its SecurityVersion still equals the expected version (and, for login, that
    /// it is not currently locked out), and only then inserts the token. The row lock is held
    /// until the transaction commits, so a concurrent credential change or lockout cannot slip
    /// between the guard and the insertion. On refusal nothing is inserted; on insertion
    /// failure the transaction is rolled back and the context is left clean.
    /// </summary>
    Task<GuardedIssuanceResult> TryIssueGuardedAsync(
        Guid userId,
        int expectedSecurityVersion,
        bool requireUnlocked,
        DateTime now,
        RefreshToken token,
        CancellationToken ct = default);

    /// <summary>
    /// Atomic. Of two requests using the same token at the same moment, only one gets true.
    /// </summary>
    Task<bool> TryRevokeAsync(Guid tokenId, DateTime now, CancellationToken ct = default);

    /// <summary>Revoking an unknown or already revoked token is not an error.</summary>
    Task RevokeByHashAsync(string tokenHash, DateTime now, CancellationToken ct = default);

    /// <summary>Ends every session of a user in one statement.</summary>
    Task<int> RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>Returns true if any token in the family is unrevoked and not expired.</summary>
    Task<bool> HasActiveTokenInFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default);

    /// <summary>Revokes every unrevoked token in the family.</summary>
    Task<int> RevokeAllInFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default);
}

/// <summary>Outcome of a guarded refresh-token issuance.</summary>
public enum GuardedIssuanceResult
{
    /// <summary>The guard passed and the token was inserted and committed.</summary>
    Issued,

    /// <summary>
    /// The user-row guard refused: the account is missing, inactive, its security version moved
    /// on (password change, reset, deactivation), or — for login — it is currently locked out.
    /// </summary>
    UserUnavailable,

    /// <summary>The token insert failed; the transaction was rolled back and nothing was stored.</summary>
    InsertFailed
}