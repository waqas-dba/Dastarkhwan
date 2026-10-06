
namespace CoreKit.IAM.Security;

/// <summary>
/// One place that decides what an acceptable password is. Length matters more than special-character
/// rules (this is also what NIST recommends), so the policy is length plus a few obvious checks.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;

    // Long enough for any passphrase, short enough that hashing it cannot be abused to slow the server.
    public const int MaxLength = 256;

    /// <summary>Returns a message for the user, or null when the password is acceptable.</summary>
    public static string? Validate(string? password, string? email = null)
    {
        if (string.IsNullOrEmpty(password))
            return "A password is required.";

        if (password.Length < MinLength)
            return $"The password must be at least {MinLength} characters long.";

        if (password.Length > MaxLength)
            return $"The password must be at most {MaxLength} characters long.";

        if (email is not null && string.Equals(password.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
            return "The password must not be the same as the email address.";

        return null;
    }
}