using CoreKit.IAM.Entities;

namespace CoreKit.IAM.Interfaces;

public interface IEmailVerificationTokenRepository
{
    /// <summary>Read-only, with the user.</summary>
    Task<EmailVerificationToken?> FindByHashWithUserAsync(string tokenHash, CancellationToken ct = default);

    Task<bool> HasCreatedSinceAsync(Guid userId, DateTime cutoff, CancellationToken ct = default);

    /// <summary>Closes the user's unused tokens and stores the new one, in one transaction.</summary>
    Task ReplaceOpenTokensAsync(EmailVerificationToken newToken, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Marks the token used and the user's email confirmed, in one transaction.
    /// Returns false when someone else already used the token.
    /// </summary>
    Task<bool> ConfirmEmailAsync(Guid tokenId, Guid userId, DateTime now, CancellationToken ct = default);
}