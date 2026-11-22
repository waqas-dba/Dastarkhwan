using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace Dastarkhwan.Web.Configuration;

/// <summary>
/// Configuration source that expands ${VAR_NAME} placeholders in connection strings
/// with values from environment variables. This allows appsettings.Production.json to
/// use placeholders like ${DB_PASSWORD} that are resolved at runtime from the environment.
/// </summary>
internal sealed class EnvironmentVariableExpanderConfigurationSource : IConfigurationSource
{
    private readonly IConfigurationRoot _root;

    public EnvironmentVariableExpanderConfigurationSource(IConfigurationRoot root)
    {
        _root = root;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new EnvironmentVariableExpanderConfigurationProvider(_root);
}

/// <summary>
/// Provider that expands ${VAR_NAME} placeholders in configuration values.
/// </summary>
internal sealed class EnvironmentVariableExpanderConfigurationProvider : ConfigurationProvider
{
    private readonly IConfigurationRoot _root;
    private static readonly Regex PlaceholderRegex = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    public EnvironmentVariableExpanderConfigurationProvider(IConfigurationRoot root)
    {
        _root = root;
    }

    public override void Load()
    {
        // Get the original configuration data from the root
        var originalData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _root.AsEnumerable())
        {
            if (kvp.Value is not null)
            {
                originalData[kvp.Key] = kvp.Value;
            }
        }

        // Expand placeholders in all values
        foreach (var kvp in originalData)
        {
            var expanded = ExpandPlaceholders(kvp.Value);
            Data[kvp.Key] = expanded;
        }
    }

    private string? ExpandPlaceholders(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return PlaceholderRegex.Replace(value, match =>
        {
            var envVarName = match.Groups[1].Value;
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            return envValue ?? match.Value; // Keep original placeholder if env var not found
        });
    }
}

/// <summary>
/// Extension method to add environment variable placeholder expansion to the configuration builder.
/// </summary>
public static class EnvironmentVariableExpanderExtensions
{
    /// <summary>
    /// Adds a configuration source that expands ${VAR_NAME} placeholders in all configuration values
    /// with values from environment variables. Should be added after all other configuration sources
    /// so it can process the final merged configuration.
    /// </summary>
    public static IConfigurationBuilder AddEnvironmentVariableExpansion(this IConfigurationBuilder builder)
    {
        // Build an intermediate root to get the current configuration state
        var intermediateRoot = builder.Build();
        builder.Add(new EnvironmentVariableExpanderConfigurationSource(intermediateRoot));
        return builder;
    }
}