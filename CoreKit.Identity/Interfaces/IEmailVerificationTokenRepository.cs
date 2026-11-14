using CoreKit.Identity.Entities;

namespace CoreKit.Identity.Interfaces;

public interface IEmailVerificationTokenRepository
{
    /// <summary>Read-only, with the user.</summary>
    Task<EmailVerificationToken?> FindByHashWithUserAsync(string tokenHash, CancellationToken ct = default);

    Task<bool> HasCreatedSinceAsync(Guid userId, DateTime cutoff, CancellationToken ct = default);

    /// <summary>Closes the user's unused tokens and stores the new one, in one transaction.</summary>
    Task ReplaceOpenTokensAsync(EmailVerificationToken newToken, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Marks the token used and the user's email confirmed, in one transaction. The confirmation
    /// is bound to the token's normalized email: the user row is only updated when its current
    /// NormalizedEmail still equals the address the token was issued for, so a token can never
    /// confirm a different address. If the bind fails, the token claim is rolled back and false
    /// is returned. Confirmation never touches SecurityVersion and never revokes sessions.
    /// Returns false when the token was already used or does not match the current user.
    /// </summary>
    Task<bool> ConfirmEmailAsync(
        Guid tokenId, Guid userId, string tokenNormalizedEmail, DateTime now, CancellationToken ct = default);
}