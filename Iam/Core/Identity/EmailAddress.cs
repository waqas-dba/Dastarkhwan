using System.Diagnostics.CodeAnalysis;

namespace Iam.Core.Identity;

/// <summary>A normalised email address (trimmed, lower-case). Can only be built through TryCreate.</summary>
public sealed record EmailAddress
{
    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? input, [NotNullWhen(true)] out EmailAddress? email)
    {
        email = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var value = input.Trim().ToLowerInvariant();
        if (value.Length > 254 || value.Contains(' ')) return false;

        var at = value.IndexOf('@');
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1) return false;

        var domain = value[(at + 1)..];
        if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.')) return false;

        email = new EmailAddress(value);
        return true;
    }

    public override string ToString() => Value;
}