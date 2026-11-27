using Npgsql;

namespace CoreKit.Access.Persistence;

/// <summary>
/// The single place that decides how Access connects to PostgreSQL. The running app and the
/// 'dotnet ef' tool both use it, so they cannot disagree.
/// </summary>
public static class AccessDbContextConfigurator
{
    /// <summary>Access keeps its own migration history in the access schema, so it never mixes with another module's.</summary>
    public const string MigrationsHistoryTable = "__AccessMigrationsHistory";
    public const string Schema = "access";

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options,
        string connectionString,
        AccessDatabaseOptions? database = null)
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

                if (database?.EnableRetryOnFailure == true)
                {
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: database.MaxRetryAttempts,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                }
            });

        return options;
    }
}