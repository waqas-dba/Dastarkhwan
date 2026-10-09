using CoreKit.IAM.Common;

namespace CoreKit.IAM.Validation;

public static class RoleInputValidator
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    public static IamError? ValidateName(string? input, out string? cleaned)
    {
        cleaned = input?.Trim();

        if (string.IsNullOrEmpty(cleaned))
            return IamErrors.Validation("A role name is required.");

        return cleaned.Length > MaxNameLength
            ? IamErrors.Validation($"The role name is too long (max {MaxNameLength} characters).")
            : null;
    }

    public static IamError? ValidateDescription(string? input, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

        return cleaned is { Length: > MaxDescriptionLength }
            ? IamErrors.Validation($"The description is too long (max {MaxDescriptionLength} characters).")
            : null;
    }
}