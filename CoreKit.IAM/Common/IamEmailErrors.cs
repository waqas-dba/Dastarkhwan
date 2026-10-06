namespace CoreKit.IAM.Common;

public static class IamEmailErrors
{
    // 403, not 401: the password was right, so the client should offer "resend the email",
    // not "wrong password" or "your session ended".
    public static readonly IamError EmailNotConfirmed = new(
        IamErrorKind.Forbidden,
        "auth.email_not_confirmed",
        "Please confirm your email address before signing in.");
}