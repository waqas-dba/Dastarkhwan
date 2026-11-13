using System.Text.RegularExpressions;

namespace CoreKit.Identity.Validation;

/// <summary>
/// Validates and normalizes mobile numbers. A mobile number is a contact channel,
/// not a login identifier — email remains the login.
/// </summary>
public static class MobileValidator
{
    public const int MaxLength = 50;

    /// <summary>At least 7 digits (E.164 minimum), at most 15 digits (E.164 maximum).</summary>
    public const int MinDigits = 7;

    public const int MaxDigits = 15;

    /// <summary>
    /// Returns the normalized mobile (digits only, optional leading '+') or null when invalid.
    /// Formatting characters (spaces, dashes, parentheses, dots) are ignored.
    /// </summary>
    public static string? Normalize(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return null;

        var trimmed = mobile.Trim();

        if (trimmed.Length > MaxLength) return null;

        var hasPlus = trimmed.StartsWith('+');
        var digits = Regex.Replace(trimmed, @"\D", string.Empty); // digits only

        if (digits.Length < MinDigits || digits.Length > MaxDigits) return null;

        return (hasPlus ? "+" : string.Empty) + digits;
    }

    /// <summary>True when the mobile number passes validation.</summary>
    public static bool IsValid(string? mobile) => Normalize(mobile) is not null;
}
