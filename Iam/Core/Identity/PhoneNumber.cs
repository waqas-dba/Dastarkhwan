using System.Diagnostics.CodeAnalysis;

namespace Iam.Core.Identity;

/// <summary>
/// A phone number in E.164 form, for example +923001234567.
/// "0300 1234567", "923001234567" and "+92 300 1234567" all become the same value,
/// so one person cannot create several accounts with different spellings.
/// </summary>
public sealed record PhoneNumber
{
    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    /// <param name="defaultCountryCode">Digits only, without '+', for example "92" for Pakistan.</param>
    public static bool TryCreate(
        string? input, string defaultCountryCode, [NotNullWhen(true)] out PhoneNumber? phone)
    {
        if (string.IsNullOrEmpty(defaultCountryCode) || !defaultCountryCode.All(char.IsAsciiDigit))
            throw new ArgumentException("The default country code must contain digits only.", nameof(defaultCountryCode));

        phone = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var raw = input.Trim();
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0) return false;

        string international;
        if (raw.StartsWith('+'))
            international = digits;                                   // +92 300 ...
        else if (digits.StartsWith("00"))
            international = digits[2..];                              // 0092 300 ...
        else if (digits.StartsWith('0'))
            international = defaultCountryCode + digits[1..];         // 0300 ...
        else if (digits.StartsWith(defaultCountryCode) && digits.Length > defaultCountryCode.Length + 6)
            international = digits;                                   // 92300 ...
        else
            international = defaultCountryCode + digits;              // 300 ...

        if (international.Length is < 8 or > 15) return false;        // E.164 allows at most 15 digits

        phone = new PhoneNumber("+" + international);
        return true;
    }

    public override string ToString() => Value;
}