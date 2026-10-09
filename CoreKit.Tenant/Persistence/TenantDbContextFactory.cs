using Microsoft.EntityFrameworkCore.Design;

namespace CoreKit.Tenant.Persistence;

/// <summary>
/// Used only by 'dotnet ef'. The connection string comes from an environment variable,
/// so no password is ever written into source code.
/// </summary>
public sealed class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Tenant");


        //var connectionString = "Host=127.0.0.1;Port=5432;Database=daskhawa;Username=postgres;Password=123";

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Set the ConnectionStrings__Tenant environment variable before running dotnet ef. " +
                "Example (PowerShell): $env:ConnectionStrings__Tenant = " +
                "\"Host=127.0.0.1;Port=5432;Database=daskhawa;Username=postgres;Password=YOUR_PASSWORD\"");

        var builder = new DbContextOptionsBuilder<TenantDbContext>();
        TenantDbContextConfigurator.Configure(builder, connectionString);

        return new TenantDbContext(builder.Options);
    }
}