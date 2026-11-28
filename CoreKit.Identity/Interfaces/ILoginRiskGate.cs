namespace CoreKit.Identity.Interfaces;

public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string token, string? remoteIp, CancellationToken ct = default);
}

/// <summary>
/// Extension point for "unusual access" signals (new country, new device, impossible travel, ...).
/// Return true to force a captcha challenge for this login attempt. None are registered by default.
/// </summary>
public interface ILoginRiskSignal
{
    Task<bool> RequiresChallengeAsync(string normalizedEmail, string? remoteIp, CancellationToken ct = default);
}

public interface ILoginRiskGate
{
    /// <summary>Success when no challenge is needed or the supplied captcha token is valid.</summary>
    Task<Common.IdentityResult> CheckAsync(string normalizedEmail, string? captchaToken, CancellationToken ct = default);

    void RecordFailure(string normalizedEmail);

    void RecordSuccess(string normalizedEmail);
}
