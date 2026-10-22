namespace CoreKit.Identity.Seeding;

public static class SeedingExtensions
{
    /// <summary>Registers the seeder. Reads "Identity:Seed" from configuration.</summary>
    public static IServiceCollection AddIdentitySeeding(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdentitySeedOptions>().Bind(configuration.GetSection(IdentitySeedOptions.SectionName));
        services.AddScoped<IdentitySeeder>();
        return services;
    }

    /// <summary>Runs the seeder once. Call after migrations, before the app starts serving.</summary>
    public static async Task SeedIdentityAsync(
        this IServiceProvider provider,
        IEnumerable<PermissionSeed>? modulePermissions = null,
        CancellationToken ct = default)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(modulePermissions, ct);
    }
}