
namespace CoreKit.Identity.Common;

public static class IdentityErrors   
{
    public static readonly IdentityError InvalidCredentials = new(
        IdentityErrorKind.Unauthorized, "auth.invalid_credentials", "The email or password is incorrect.");

    public static readonly IdentityError InvalidRefreshToken = new(
        IdentityErrorKind.Unauthorized, "auth.invalid_refresh_token", "The refresh token is not valid. Please sign in again.");

    public static readonly IdentityError NotAuthenticated = new(
        IdentityErrorKind.Unauthorized, "auth.not_authenticated", "You are not signed in.");

    // 400, not 401: a clients treats 401 as "your session ended", which is not what happened here.
    public static readonly IdentityError WrongCurrentPassword = new(
        IdentityErrorKind.Validation, "auth.wrong_current_password", "The current password is incorrect.");

    public static IdentityError Validation(string message)
        => new(IdentityErrorKind.Validation, "validation.failed", message);

    public static IdentityError NotFound(string what)
        => new(IdentityErrorKind.NotFound, "not_found", $"{what} was not found.");

    public static IdentityError Conflict(string message)
        => new(IdentityErrorKind.Conflict, "conflict", message);

    /// <summary>
    /// The caller is signed in but lacks the permission the operation requires. Kept separate from
    /// <see cref="Forbidden"/> so clients can tell "you may not do this at all" from "this specific
    /// change is not allowed" (for example editing the protected Administrator role).
    /// </summary>
    public static IdentityError AccessDenied(string message)
        => new(IdentityErrorKind.Forbidden, "access.denied", message);

    public static IdentityError Forbidden(string message)
        => new(IdentityErrorKind.Forbidden, "forbidden", message);

    /// <summary>One message for every password-reset token failure, so the response never says which part was wrong.</summary>
    public static readonly IdentityError InvalidResetToken = new(
        IdentityErrorKind.Validation, "auth.invalid_reset_token", "The password reset link is invalid or has expired.");

    /// <summary>
    /// No longer returned by login: a distinct "locked" answer would reveal which emails have accounts
    /// (see F-04). Login answers <see cref="InvalidCredentials"/> for a locked account and records the
    /// lockout in the server log. Kept only for callers that build their own account-management flows.
    /// </summary>
    public static readonly IdentityError AccountLocked = new(
        IdentityErrorKind.Forbidden, "auth.account_locked", "The account is temporarily locked due to too many failed attempts. Please try again later.");
}
