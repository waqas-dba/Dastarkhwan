using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CoreKit.Tenant.Services;

/// <summary>
/// One place that decides what a valid slug is. A slug is also a valid DNS label, so it can safely become
/// part of a sub-domain later.
/// </summary>
internal static class TenantSlug
{
    public const int MinLength = 3;
    public const int MaxLength = 63;

    private static readonly Regex Pattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Normalize(string slug) => slug.Trim().ToLowerInvariant();

    /// <summary>Returns a message, or null when the (already normalized) slug is acceptable.</summary>
    public static string? Validate(string slug, IReadOnlyCollection<string> reserved)
    {
        if (slug.Length < MinLength || slug.Length > MaxLength)
            return $"The slug must be {MinLength} to {MaxLength} characters long.";

        if (!Pattern.IsMatch(slug))
            return "The slug may only contain lowercase letters, digits and single hyphens, " +
                   "and must start and end with a letter or digit.";

        if (reserved.Contains(slug, StringComparer.OrdinalIgnoreCase))
            return $"The slug '{slug}' is reserved.";

        return null;
    }

    /// <summary>"Café Délice!" becomes "cafe-delice". Names with no Latin letters or digits give an empty string.</summary>
    public static string FromName(string name)
    {
        var builder = new StringBuilder();

        foreach (var ch in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
                builder.Append(ch);
            else if (ch is >= 'A' and <= 'Z')
                builder.Append(char.ToLowerInvariant(ch));
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        var slug = builder.ToString().Trim('-');

        return slug.Length > MaxLength ? slug[..MaxLength].Trim('-') : slug;
    }
}