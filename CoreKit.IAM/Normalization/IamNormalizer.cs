using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Normalization;

/// <summary>
/// One place that decides what "the same email" or "the same role name" means.
/// "Waqas.Qazi@Example.COM" and " waqas.qazi@example.com " become the same value.
/// </summary>
public static class IamNormalizer
{
    public static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return email.Trim().ToUpperInvariant();
    }

    public static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim().ToUpperInvariant();
    }
}
