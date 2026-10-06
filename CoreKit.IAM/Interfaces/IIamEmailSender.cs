namespace CoreKit.IAM.Interfaces;

/// <summary>What the host needs to build and send the verification email.</summary>
public sealed record EmailVerificationMessage(
    Guid UserId,
    string Email,
    string? DisplayName,
    string Token,
    DateTime ExpiresAt);

/// <summary>
/// IAM never sends email itself. The host app implements this with its own provider
/// (SMTP, SendGrid, ...) and builds the link, for example https://app/verify-email?token={Token}.
/// </summary>
public interface IIamEmailSender
{
    Task SendEmailVerificationAsync(EmailVerificationMessage message, CancellationToken ct = default);
}