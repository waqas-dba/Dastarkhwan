using CoreKit.Identity.Common;

namespace CoreKit.Identity.Validation;

public static class DisplayNameValidator
{
    public const int MaxLength = 200;

    public static Common.IdentityError? Validate(string? input, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

        return cleaned is { Length: > MaxLength }
            ? IdentityErrors.Validation($"The display name is too long (max {MaxLength} characters).")
            : null;
    }
}