namespace CoreKit.Identity.Settings;

public enum CaptchaMode
{
    /// <summary>Never ask for a captcha (default).</summary>
    Off = 0,
    /// <summary>Ask only after repeated failures for an e-mail / from an IP, or when a risk signal fires.</summary>
    OnSuspicious = 1,
    /// <summary>Ask on every login.</summary>
    Always = 2
}

public sealed class IdentityCaptchaOptions
{
    public const string SectionName = "Identity:Captcha";

    public CaptchaMode Mode { get; set; } = CaptchaMode.Off;

    /// <summary>siteverify endpoint (Turnstile by default; hCaptcha / reCAPTCHA use the same protocol).</summary>
    public string VerifyUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Failed logins for one e-mail inside the window before a challenge is required.</summary>
    public int EmailFailureThreshold { get; set; } = 3;

    /// <summary>Failed logins from one IP inside the window before a challenge is required.</summary>
    public int IpFailureThreshold { get; set; } = 10;

    public int WindowMinutes { get; set; } = 15;

    public int VerifyTimeoutSeconds { get; set; } = 5;
}
