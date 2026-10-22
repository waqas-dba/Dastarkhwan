using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.Identity.Settings;

public static class IdentityRateLimiting
{
    public const string AuthPolicy = "identity-auth";
}

public sealed class IdentityRateLimitOptions
{
    public const string SectionName = "Identity:RateLimit";

    /// <summary>Turn off only for tests or load tests.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How many login, refresh or password requests one IP address may make per window.</summary>
    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;
}