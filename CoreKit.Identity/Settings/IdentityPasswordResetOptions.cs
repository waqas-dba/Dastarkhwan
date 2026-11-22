namespace CoreKit.Identity.Settings;

public sealed class IdentityPasswordResetOptions
{
    public const string SectionName = "Identity:PasswordReset";

    /// <summary>How long a password-reset token stays valid, in minutes.</summary>
    public int TokenMinutes { get; set; } = 60;
}
