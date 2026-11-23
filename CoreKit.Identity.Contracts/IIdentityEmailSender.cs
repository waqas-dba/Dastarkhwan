namespace CoreKit.Identity.Interfaces;

/// <summary>
/// Identity never sends email itself. The host app implements this with its own provider
/// (SMTP, SendGrid, ...) and builds the link, for example https://app/verify-email?token={Token}
/// or https://app/reset-password?token={Token}.
/// Implementations should queue the message and return quickly: a slow send for existing accounts and an
/// instant return for unknown ones would let a caller tell them apart by response time.
/// </summary>
public interface IIdentityEmailSender
{
    Task SendEmailVerificationAsync(EmailVerificationMessage message, CancellationToken ct = default);

    Task SendPasswordResetAsync(PasswordResetMessage message, CancellationToken ct = default);
}

/// <summary>What the host needs to build and send the verification email.</summary>
public sealed record EmailVerificationMessage(
    Guid UserId,
    string Email,
    string? DisplayName,
    string Token,
    DateTime ExpiresAt);

/// <summary>What the host needs to build and send the password-reset email.</summary>
public sealed record PasswordResetMessage(
    Guid UserId,
    string Email,
    string? DisplayName,
    string Token,
    DateTime ExpiresAt);
