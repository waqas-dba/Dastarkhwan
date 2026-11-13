namespace CoreKit.Identity.Validation;

/// <summary>
/// Validates and normalizes mobile numbers. A mobile number is a contact channel,
/// not a login identifier — email remains the login.
///
/// Validation is strict: only digits, an optional single leading '+', and the
/// formatting characters space, dash, parentheses and dot are permitted. Any
/// other character (letters, symbols, misplaced '+') causes rejection — invalid
/// characters are never silently stripped.
///
/// Storage is canonical: digits only, with an optional leading '+'. Local-format
/// numbers (for example a Pakistani number starting with '0') are stored as-is
/// without country-code conversion, because no country-code setting exists in
/// configuration. Converting local input to E.164 (for example '03001234567'
/// → '+923001234567') requires a country code that must be configured separately;
/// see the project backlog for that decision.
/// </summary>
public static class MobileValidator
{
    public const int MaxLength = 50;

    /// <summary>At least 7 digits (E.164 minimum), at most 15 digits (E.164 maximum).</summary>
    public const int MinDigits = 7;

    public const int MaxDigits = 15;

    /// <summary>Formatting characters that are permitted around a mobile number.</summary>
    private static readonly HashSet<char> FormattingChars = new() { ' ', '-', '(', ')', '.' };

    /// <summary>
    /// Returns the normalized mobile (digits only, optional leading '+') or null when invalid.
    /// Formatting characters are stripped; invalid characters cause rejection.
    /// </summary>
    public static string? Normalize(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return null;

        var trimmed = mobile.Trim();

        if (trimmed.Length > MaxLength) return null;

        var hasPlus = false;
        var digitCount = 0;
        var digitSeen = false;

        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];

            if (c >= '0' && c <= '9')
            {
                digitCount++;
                digitSeen = true;
                continue;
            }

            if (c == '+')
            {
                // Plus is allowed only once, and only before any digit has appeared.
                // Leading formatting characters (for example an opening parenthesis)
                // may precede it, as in "(+44) 7700 900123".
                if (hasPlus || digitSeen) return null;
                hasPlus = true;
                continue;
            }

            if (FormattingChars.Contains(c)) continue;

            // Any other character (letters, symbols, internal '+', etc.) → reject.
            return null;
        }

        if (digitCount < MinDigits || digitCount > MaxDigits) return null;

        var digits = new string(trimmed.Where(c => c >= '0' && c <= '9').ToArray());
        return (hasPlus ? "+" : string.Empty) + digits;
    }

    /// <summary>True when the mobile number passes validation.</summary>
    public static bool IsValid(string? mobile) => Normalize(mobile) is not null;
}
