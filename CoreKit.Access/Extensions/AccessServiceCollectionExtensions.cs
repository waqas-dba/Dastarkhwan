using CoreKit.Access.Catalog;

namespace CoreKit.Access.Extensions;

public static class AccessServiceCollectionExtensions
{
    public const string ConnectionStringName = "Access";

    /// <summary>
    /// Registers Access: database, repositories, services, and defaults that read the current user, tenant and
    /// tenant membership through the shared kernel. Without a Tenant module nobody belongs to a scope, so
    /// nothing is granted. Reads "ConnectionStrings:Access" and "Access".
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
        services.AddScoped<IAccessAuditLog, AccessAuditLog>();

        // What Access offers other modules, through the shared kernel.
        services.AddScoped<IPermissionChecker, AccessPermissionChecker>();

        // What Access reads from other modules, through the shared kernel. Each default works with or
        // without the Tenant and identity modules: a missing one simply means "no scope" or "nobody belongs".
        services.TryAddScoped<ICurrentUser, ClaimsCurrentUser>();
        services.TryAddScoped<IAccessScopeContext, TenantAccessScopeContext>();
        services.TryAddScoped<IAccessMembership, TenantAccessMembership>();

        // React to tenant facts: roles for a new tenant, cleanup when someone leaves or the tenant is deleted.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEventHandler<TenantCreated>, TenantCreatedHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEventHandler<TenantMemberRemoved>, TenantMemberRemovedHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEventHandler<TenantDeleted>, TenantDeletedHandler>());

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