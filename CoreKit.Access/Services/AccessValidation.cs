namespace CoreKit.Access.Services;

internal static class AccessValidation
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    public static AccessError? ValidateName(string? input, out string? cleaned)
    {
        cleaned = input?.Trim();

        if (string.IsNullOrEmpty(cleaned))
            return AccessErrors.Validation("A role name is required.");

        return cleaned.Length > MaxNameLength
            ? AccessErrors.Validation($"The role name is too long (max {MaxNameLength} characters).")
            : null;
    }

    public static AccessError? ValidateDescription(string? input, out string? cleaned)
    {
        cleaned = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

        return cleaned is { Length: > MaxDescriptionLength }
            ? AccessErrors.Validation($"The description is too long (max {MaxDescriptionLength} characters).")
            : null;
    }

    /// <summary>Every permission must be declared in the catalog. An empty list is fine.</summary>
    public static AccessError? ValidatePermissions(IEnumerable<string>? input, AccessCatalog catalog, out List<string> cleaned)
    {
        cleaned = new List<string>();

        foreach (var raw in input ?? Enumerable.Empty<string>())
        {
            var permission = raw?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(permission))
                return AccessErrors.Validation("A permission name cannot be empty.");

            if (!catalog.Contains(permission))
                return AccessErrors.Validation($"The permission '{permission}' does not exist.");

            if (!cleaned.Contains(permission))
                cleaned.Add(permission);
        }

        return null;
    }
}