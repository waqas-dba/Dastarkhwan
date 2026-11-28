using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.Identity.Persistence;

/// <summary>
/// Used only by 'dotnet ef'. The connection string comes from an environment
/// variable, so no password is ever written into source code.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Set the ConnectionStrings__Default environment variable before running dotnet ef. " +
                "Example (PowerShell): $env:ConnectionStrings__Default = " +
                "\"Host=127.0.0.1;Port=5432;Database=dastarkhwan_db;Username=postgres;Password=YOUR_PASSWORD\"");

        var builder = new DbContextOptionsBuilder<IdentityDbContext>();
        IdentityDbContextConfigurator.Configure(builder, connectionString);

        return new IdentityDbContext(builder.Options);
    }
}
