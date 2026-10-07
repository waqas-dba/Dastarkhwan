namespace CoreKit.Tenant.Persistence;

/// <summary>
/// The single place that decides how Tenant connects to PostgreSQL. The running app and the
/// 'dotnet ef' tool both use it, so they cannot disagree.
/// </summary>
public static class TenantDbContextConfigurator
{
    /// <summary>Tenant keeps its own migration history, so it never mixes with IAM's or another module's.</summary>
    public const string MigrationsHistoryTable = "__TenantMigrationsHistory";

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString)
        => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable));
}