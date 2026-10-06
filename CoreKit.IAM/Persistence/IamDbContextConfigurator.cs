using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Persistence;

/// <summary>
/// The single place that decides how IAM connects to PostgreSQL. The running app and
/// the 'dotnet ef' tool both use it, so they cannot disagree.
/// </summary>
public static class IamDbContextConfigurator
{
    /// <summary>
    /// IAM keeps its own migration history table, so later modules that share the
    /// same database never mix their migrations with IAM's.
    /// </summary>
    public const string MigrationsHistoryTable = "__IamMigrationsHistory";

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString)
        => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable));
}