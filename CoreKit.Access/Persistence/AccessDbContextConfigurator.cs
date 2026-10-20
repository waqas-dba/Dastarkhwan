namespace CoreKit.Access.Persistence;

/// <summary>
/// The single place that decides how Access connects to PostgreSQL. The running app and the
/// 'dotnet ef' tool both use it, so they cannot disagree.
/// </summary>
public static class AccessDbContextConfigurator
{
    /// <summary>Access keeps its own migration history, so it never mixes with another module's.</summary>
    public const string MigrationsHistoryTable = "__AccessMigrationsHistory";

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString)
        => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable));
}