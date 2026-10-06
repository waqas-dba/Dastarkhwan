using System.Net.Mail;

namespace CoreKit.IAM.Validation;

public static class EmailValidator
{
    public const int MaxLength = 320;

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var value = email.Trim();
        if (value.Length > MaxLength) return false;

        if (!MailAddress.TryCreate(value, out var address)) return false;

        // MailAddress also accepts forms like "Ali <ali@example.com>". We only want the plain address.
        if (!string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase)) return false;

        return address.Host.Contains('.');
    }
}
