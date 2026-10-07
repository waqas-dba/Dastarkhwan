using CoreKit.IAM.Common;

namespace CoreKit.IAM.Validation;

internal static class DisplayNameValidator
{
    public const int MaxLength = 200;

    public static IamError? Validate(string? input, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

        return cleaned is { Length: > MaxLength }
            ? IamErrors.Validation($"The display name is too long (max {MaxLength} characters).")
            : null;
    }
}