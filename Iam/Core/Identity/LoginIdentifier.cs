using System.Diagnostics.CodeAnalysis;

namespace Iam.Core.Identity;

public enum LoginIdentifierKind
{
    Email = 1,
    Phone = 2
}

/// <summary>
/// What a person types into the login box: an email or a phone number.
/// Contains '@' means email, otherwise phone.
/// </summary>
public sealed record LoginIdentifier(LoginIdentifierKind Kind, string NormalizedValue)
{
    public static bool TryParse(
        string? input, string defaultCountryCode, [NotNullWhen(true)] out LoginIdentifier? identifier)
    {
        identifier = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        if (input.Contains('@'))
        {
            if (!EmailAddress.TryCreate(input, out var email)) return false;
            identifier = new LoginIdentifier(LoginIdentifierKind.Email, email.Value);
            return true;
        }

        if (!PhoneNumber.TryCreate(input, defaultCountryCode, out var phone)) return false;
        identifier = new LoginIdentifier(LoginIdentifierKind.Phone, phone.Value);
        return true;
    }
}