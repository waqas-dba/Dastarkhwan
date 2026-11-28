namespace CoreKit.Identity.Common;

public static class IdentityCaptchaErrors
{
    /// <summary>The client should render its captcha widget and resend the login with CaptchaToken.</summary>
    public static readonly Common.IdentityError Required = new(
        IdentityErrorKind.Forbidden, "auth.captcha_required",
        "Please complete the captcha challenge and try again.");

    public static readonly Common.IdentityError Failed = new(
        IdentityErrorKind.Forbidden, "auth.captcha_failed",
        "The captcha could not be verified. Please try again.");
}
