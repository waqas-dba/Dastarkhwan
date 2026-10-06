namespace CoreKit.IAM.Interfaces;

public interface IEmailVerificationService
{
    /// <summary>Sends a verification email to the signed-in user. Respects the resend cooldown.</summary>
    Task<IamResult> SendToCurrentUserAsync(CancellationToken ct = default);

    /// <summary>Sends a verification email to a given user (for administrators or the host app). No cooldown.</summary>
    Task<IamResult> SendAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// For someone who cannot sign in yet because the email is unconfirmed. Always succeeds, whether or not
    /// the address has an account, so the response never reveals which emails are registered.
    /// </summary>
    Task<IamResult> ResendByEmailAsync(ResendVerificationRequest request, CancellationToken ct = default);

    /// <summary>Confirms the email address that the token was issued for.</summary>
    Task<IamResult> VerifyAsync(VerifyEmailRequest request, CancellationToken ct = default);
}