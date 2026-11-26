namespace CoreKit.Identity.Services;

/// <summary>Standardized reasons for revoking a refresh token.</summary>
public static class RefreshTokenRevokedReasons
{
    public const string Rotated = "rotated";
    public const string ReplayDetected = "replay_detected";
    public const string Logout = "logout";
    public const string PasswordChanged = "password_changed";
    public const string AdminPasswordChange = "admin_password_change";
    public const string UserDeactivated = "user_deactivated";
    public const string EmailNotConfirmed = "email_not_confirmed";
}
