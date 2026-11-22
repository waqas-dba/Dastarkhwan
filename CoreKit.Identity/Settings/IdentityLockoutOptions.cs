namespace CoreKit.Identity.Settings;

public sealed class IdentityLockoutOptions
{
    public const string SectionName = "Identity:Lockout";

    /// <summary>Consecutive failed logins that lock the account.</summary>
    public int Threshold { get; set; } = 5;

    /// <summary>How long a lockout lasts, in minutes.</summary>
    public int DurationMinutes { get; set; } = 15;
}
