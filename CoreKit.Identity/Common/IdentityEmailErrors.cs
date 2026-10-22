namespace CoreKit.Identity.Common;

public static class IdentityEmailErrors
{
    // 403, not 401: the password was right, so the client should offer "resend the email",
    // not "wrong password" or "your session ended".
    public static readonly IdentityError EmailNotConfirmed = new(
        IdentityErrorKind.Forbidden,
        "auth.email_not_confirmed",
        "Please confirm your email address before signing in.");
}