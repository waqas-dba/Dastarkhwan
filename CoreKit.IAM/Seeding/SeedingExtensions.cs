namespace CoreKit.IAM.Seeding;

public static class SeedingExtensions
{
    /// <summary>Registers the seeder. Reads "Iam:Seed" from configuration.</summary>
    public static IServiceCollection AddIamSeeding(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IamSeedOptions>().Bind(configuration.GetSection(IamSeedOptions.SectionName));
        services.AddScoped<IamSeeder>();
        return services;
    }

    /// <summary>Runs the seeder once. Call after migrations, before the app starts serving.</summary>
    public static async Task SeedIamAsync(
        this IServiceProvider provider,
        IEnumerable<PermissionSeed>? modulePermissions = null,
        CancellationToken ct = default)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IamSeeder>().SeedAsync(modulePermissions, ct);
    }
}