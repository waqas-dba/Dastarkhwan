using Npgsql;

namespace CoreKit.Identity.Persistence;

/// <summary>
/// The single place that decides how Identity connects to PostgreSQL. The running app and
/// the 'dotnet ef' tool both use it, so they cannot disagree.
/// </summary>
public static class IdentityDbContextConfigurator
{
    /// <summary>
    /// Identity keeps its own migration history table in the identity schema, so later modules that share the
    /// same database never mix their migrations with Identity's.
    /// </summary>
    public const string MigrationsHistoryTable = "__IdentityMigrationsHistory";
    public const string Schema = "identity";

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