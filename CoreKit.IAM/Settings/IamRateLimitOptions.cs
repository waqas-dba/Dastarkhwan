using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Settings;

public static class IamRateLimiting
{
    public const string AuthPolicy = "iam-auth";
}

public sealed class IamRateLimitOptions
{
    public const string SectionName = "Iam:RateLimit";

    /// <summary>Turn off only for tests or load tests.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How many login, refresh or password requests one IP address may make per window.</summary>
    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;
}