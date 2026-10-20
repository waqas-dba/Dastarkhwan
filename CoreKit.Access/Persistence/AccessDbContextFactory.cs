using Microsoft.EntityFrameworkCore.Design;

namespace CoreKit.Access.Persistence;

/// <summary>
/// Used only by 'dotnet ef'. The connection string comes from an environment variable,
/// so no password is ever written into source code.
/// </summary>
public sealed class AccessDbContextFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
    public AccessDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Access");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Set the ConnectionStrings__Access environment variable before running dotnet ef. " +
                "Example (PowerShell): $env:ConnectionStrings__Access = " +
                "\"Host=127.0.0.1;Port=5432;Database=daskhawa;Username=postgres;Password=YOUR_PASSWORD\"");

        var builder = new DbContextOptionsBuilder<AccessDbContext>();
        AccessDbContextConfigurator.Configure(builder, connectionString);

        return new AccessDbContext(builder.Options);
    }
}