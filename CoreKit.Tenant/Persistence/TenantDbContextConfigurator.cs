using CoreKit.SharedKernel;
using Npgsql;

namespace CoreKit.Tenant.Persistence;

/// <summary>
/// The single place that decides how Tenant connects to PostgreSQL. The running app and the
/// 'dotnet ef' tool both use it, so they cannot disagree.
/// </summary>
public static class TenantDbContextConfigurator
{
    /// <summary>Tenant keeps its own migration history, so it never mixes with Identity's or another module's.</summary>
    public const string MigrationsHistoryTable = "__TenantMigrationsHistory";

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
                npgsql.MigrationsHistoryTable(MigrationsHistoryTable);

                if (database?.EnableRetryOnFailure == true)
                    npgsql.EnableRetryOnFailure(database.MaxRetryAttempts);
            });

        return options;
    }
}