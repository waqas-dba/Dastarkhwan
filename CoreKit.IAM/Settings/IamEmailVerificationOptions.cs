namespace CoreKit.IAM.Settings;

public sealed class IamEmailVerificationOptions
{
    public const string SectionName = "Iam:EmailVerification";

    /// <summary>
    /// When true, a user whose email is not confirmed cannot sign in or refresh a session.
    /// Off by default: turn it on for apps where people register themselves, leave it off
    /// where an administrator creates the accounts.
    /// </summary>
    public bool RequireConfirmedEmail { get; set; } = false;

    /// <summary>How long a verification token stays valid.</summary>
    public int TokenHours { get; set; } = 24;

    /// <summary>Minimum wait before the same user may ask for another email.</summary>
    public int ResendCooldownSeconds { get; set; } = 60;
}