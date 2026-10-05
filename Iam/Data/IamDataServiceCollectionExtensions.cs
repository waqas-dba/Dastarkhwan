using Iam.Core.Abstractions;
using Iam.Data.Persistence;
using Iam.Data.Repositories;
using Iam.Data.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Iam.Data;

public static class IamDataServiceCollectionExtensions
{
    /// <summary>Registers the IAM database, repositories and seeder.</summary>
    public static IServiceCollection AddIamData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IamDbContext>(options =>
            IamDbContextConfigurator.Configure(options, connectionString));

        // One clock for the whole app. Tests can replace it with a fake clock.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IamDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IClientApplicationRepository, ClientApplicationRepository>();

        services.AddScoped<IamSeeder>();

        return services;
    }
}