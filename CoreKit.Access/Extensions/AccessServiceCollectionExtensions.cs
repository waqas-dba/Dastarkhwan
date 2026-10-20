using CoreKit.Access.Catalog;

namespace CoreKit.Access.Extensions;

public static class AccessServiceCollectionExtensions
{
    public const string ConnectionStringName = "Access";

    /// <summary>
    /// Registers Access: database, repositories, services and defaults for the three ports. On its own it
    /// grants nothing, because nobody is a member of anything. Add a bridge (for example for Tenant) to give
    /// it a scope and membership. Reads "ConnectionStrings:Access" and "Access".
    /// </summary>
    /// <param name="configureDatabase">
    /// Optional. Replaces the PostgreSQL setup, which is how tests plug in another database.
    /// </param>
    public static IServiceCollection AddAccess(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDatabase = null)
    {
        services.AddOptions<AccessOptions>()
            .Bind(configuration.GetSection(AccessOptions.SectionName))
            .Validate(o => o.MaxRolesPerScope >= 1, "Access:MaxRolesPerScope must be at least 1.")
            .Validate(o => o.MaxRolesPerUser >= 1, "Access:MaxRolesPerUser must be at least 1.")
            .Validate(o => o.CacheSeconds >= 0, "Access:CacheSeconds cannot be negative.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserIdClaimType), "Access:UserIdClaimType must be set.");

        services.AddDbContext<AccessDbContext>(options =>
        {
            if (configureDatabase is not null)
            {
                configureDatabase(options);
                return;
            }

            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is missing. " +
                    "Set ConnectionStrings:Access (user-secrets or the ConnectionStrings__Access environment variable).");

            AccessDbContextConfigurator.Configure(options, connectionString);
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        // Every permission and role template declared through AddAccessPermissions.
        services.TryAddSingleton(sp => AccessCatalog.Build(sp.GetServices<AccessCatalogContribution>()));

        // Persistence
        services.AddScoped<IAccessUnitOfWork, AccessUnitOfWork>();
        services.AddScoped<IAccessRoleRepository, AccessRoleRepository>();
        services.AddScoped<IAccessAssignmentRepository, AccessAssignmentRepository>();
        services.AddScoped<IAccessAuditRepository, AccessAuditRepository>();

        // Services
        services.AddSingleton<IAccessCache, AccessCache>();
        services.AddScoped<IAccessAuditRecorder, AccessAuditRecorder>();
        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<AccessGuard>();
        services.AddScoped<IAccessRoleService, AccessRoleService>();
        services.AddScoped<IAccessAssignmentService, AccessAssignmentService>();
        services.AddScoped<IAccessMaintenance, AccessMaintenance>();

        // Defaults for the ports. A bridge replaces the scope and membership ones.
        services.TryAddScoped<IAccessActor, ClaimsAccessActor>();
        services.TryAddScoped<IAccessScopeContext, NoAccessScopeContext>();
        services.TryAddScoped<IAccessMembership, NoAccessMembership>();

        return services;
    }

    /// <summary>
    /// Declares the permissions an application or module uses, and optionally the roles every new scope
    /// starts with. Can be called by several modules; everything is combined.
    /// </summary>
    public static IServiceCollection AddAccessPermissions(
        this IServiceCollection services, Action<AccessCatalogBuilder> configure)
    {
        services.AddSingleton(new AccessCatalogContribution(configure));
        return services;
    }
}