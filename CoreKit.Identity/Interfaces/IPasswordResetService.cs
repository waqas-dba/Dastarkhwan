using CoreKit.Identity.Models;

namespace CoreKit.Identity.Interfaces;

public interface IPasswordResetService
{
    /// <summary>
    /// Starts a password reset for an email address. Always succeeds from the caller's point of view, so the
    /// response never reveals whether an account exists. The email is sent only for an active account.
    /// </summary>
    Task<Common.IdentityResult> RequestAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    /// <summary>Completes a reset with the emailed token. Ends all of the user's sessions and clears any lockout.</summary>
    Task<Common.IdentityResult> ResetAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
