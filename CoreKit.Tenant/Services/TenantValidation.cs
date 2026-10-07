namespace CoreKit.Tenant.Services;

internal static class TenantValidation
{
    public const int MaxNameLength = 200;
    public const int MaxReasonLength = 500;
    public const int MaxSettingKeyLength = 100;
    public const int MaxSettingValueLength = 4000;

    public static TenantError? ValidateName(string? input, out string? cleaned)
    {
        cleaned = input?.Trim();

        if (string.IsNullOrEmpty(cleaned))
            return TenantErrors.Validation("A tenant name is required.");

        return cleaned.Length > MaxNameLength
            ? TenantErrors.Validation($"The tenant name is too long (max {MaxNameLength} characters).")
            : null;
    }

    public static TenantError? ValidateReason(string? input, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

        return cleaned is { Length: > MaxReasonLength }
            ? TenantErrors.Validation($"The reason is too long (max {MaxReasonLength} characters).")
            : null;
    }
}