using Microsoft.EntityFrameworkCore;

namespace Iam.Data.Persistence;

/// <summary>
/// The one place that decides how IAM connects to PostgreSQL. The running app and the
/// design-time factory (used by 'dotnet ef') both call it, so they can never disagree.
/// </summary>
public static class IamDbContextConfigurator
{
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString)
        => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IamDbContext.Schema));
}