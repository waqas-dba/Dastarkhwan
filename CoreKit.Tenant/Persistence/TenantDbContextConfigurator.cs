using Npgsql;

namespace CoreKit.Tenant.Persistence;

/// <summary>
/// The single place that decides how Tenant connects to PostgreSQL. The running app and the
/// 'dotnet ef' tool both use it, so they cannot disagree.
/// </summary>
public static class TenantDbContextConfigurator
{
    /// <summary>Tenant keeps its own migration history in the tenant schema, so it never mixes with Identity's or another module's.</summary>
    public const string MigrationsHistoryTable = "__TenantMigrationsHistory";
    public const string Schema = "tenant";

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options,
        string connectionString,
        DatabaseOptions? database = null)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (database?.MaxPoolSize is > 0)
            builder.MaxPoolSize = database.MaxPoolSize.Value;

        if (database?.CommandTimeoutSeconds is > 0)
            builder.CommandTimeout = database.CommandTimeoutSeconds.Value;

        options.UseNpgsql(
            builder.ConnectionString,
            npgsql =>
            {
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable, Schema);

                // Retry on failure is explicitly disabled because Tenant uses manual transactions
                // (BeginTransactionAsync) which are not compatible with EF Core's retry strategy.
                // If transient failure handling is needed, use IExecutionStrategy.ExecuteAsync for
                // specific read-only operations instead.
            });

        return options;
    }
}