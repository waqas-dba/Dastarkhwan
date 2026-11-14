namespace CoreKit.Access.Persistence;

/// <summary>
/// Connection and command settings for the Access DbContext.
/// Bound from the "Database" configuration section.
/// </summary>
public sealed class AccessDatabaseOptions
{
    public int? MaxPoolSize { get; set; }

    public int? CommandTimeoutSeconds { get; set; }

    public bool EnableRetryOnFailure { get; set; }

    public int MaxRetryAttempts { get; set; } = 6;
}