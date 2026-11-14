namespace CoreKit.Access.Common;

/// <summary>
/// Route-prefix helpers shared by every module's endpoint mapping.
/// </summary>
public static class EndpointRoutes
{
    /// <summary>"Identity", "/Identity/" and "/Identity" all become "/Identity". An empty prefix stays empty.</summary>
    public static string NormalizePrefix(string? prefix)
    {
        var trimmed = (prefix ?? string.Empty).Trim().TrimEnd('/');

        return trimmed.Length == 0 || trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }
}