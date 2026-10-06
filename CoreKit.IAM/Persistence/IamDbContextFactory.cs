using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Persistence;

/// <summary>
/// Used only by 'dotnet ef'. The connection string comes from an environment
/// variable, so no password is ever written into source code.
/// </summary>
public sealed class IamDbContextFactory : IDesignTimeDbContextFactory<IamDbContext>
{
    public IamDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Iam");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Set the ConnectionStrings__Iam environment variable before running dotnet ef. " +
                "Example (PowerShell): $env:ConnectionStrings__Iam = " +
                "\"Host=127.0.0.1;Port=5432;Database=daskhawa;Username=postgres;Password=YOUR_PASSWORD\"");

        var builder = new DbContextOptionsBuilder<IamDbContext>();
        IamDbContextConfigurator.Configure(builder, connectionString);

        return new IamDbContext(builder.Options);
    }
}
