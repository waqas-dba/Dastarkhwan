using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Iam.Data.Persistence;

/// <summary>
/// Used only by 'dotnet ef' to create migrations. It reads the connection string from an
/// environment variable, so no password is ever written into source code.
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
                "\"Host=127.0.0.1;Port=5432;Database=iam_db;Username=postgres;Password=YOUR_PASSWORD\"");

        var builder = new DbContextOptionsBuilder<IamDbContext>();
        IamDbContextConfigurator.Configure(builder, connectionString);

        return new IamDbContext(builder.Options);
    }
}