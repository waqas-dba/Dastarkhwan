using CoreKit.Identity.Common;

namespace CoreKit.Identity.Validation;

public static class RoleInputValidator
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    public static Common.IdentityError? ValidateName(string? input, out string? cleaned)
    {
        cleaned = input?.Trim();

        if (string.IsNullOrEmpty(cleaned))
            return IdentityErrors.Validation("A role name is required.");

        return cleaned.Length > MaxNameLength
            ? IdentityErrors.Validation($"The role name is too long (max {MaxNameLength} characters).")
            : null;
    }

    public static Common.IdentityError? ValidateDescription(string? input, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

        return cleaned is { Length: > MaxDescriptionLength }
            ? IdentityErrors.Validation($"The description is too long (max {MaxDescriptionLength} characters).")
            : null;
    }
}