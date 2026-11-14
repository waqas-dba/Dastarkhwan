namespace CoreKit.Tenant.Persistence;

/// <summary>
/// Connection and command settings for the Tenant DbContext.
/// Bound from the "Database" configuration section. All values are optional;
/// unset values leave Npgsql/EF Core defaults in place.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Maximum number of pooled connections per DbContext (Npgsql default: 100).
    /// Set to 0 or leave unset to keep the Npgsql default.
    /// </summary>
    public int? MaxPoolSize { get; set; }

    /// <summary>
    /// Seconds a command may run before it is cancelled (Npgsql default: 30).
    /// Set to 0 or leave unset to keep the Npgsql default.
    /// </summary>
    public int? CommandTimeoutSeconds { get; set; }

    /// <summary>
    /// When true, transient Npgsql failures are retried automatically (EF Core default: off).
    /// </summary>
    public bool EnableRetryOnFailure { get; set; }

    /// <summary>Maximum retry attempts when <see cref="EnableRetryOnFailure"/> is true (EF Core default: 6).</summary>
    public int MaxRetryAttempts { get; set; } = 6;
}