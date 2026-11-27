using CoreKit.Access.Abstractions;
using CoreKit.Access.Persistence;
using CoreKit.Access.Services;
using Microsoft.Extensions.Hosting;

namespace CoreKit.Access.Extensions;

public static class AccessServiceCollectionExtensions
{
    public const string ConnectionStringName = "Default";

    /// <summary>
    /// Registers Access: database, repositories, services, and defaults that read the current user and
    /// scope through Access's own abstractions. Without a scope module nobody belongs to a scope, so
    /// nothing is granted. Reads "ConnectionStrings:Default" and "Access".
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

        services.AddOptions<AccessDatabaseOptions>()
            .Bind(configuration.GetSection("Database"))
            .Validate(o => o.MaxPoolSize is null or > 0, "Database:MaxPoolSize must be positive when set.")
            .Validate(o => o.CommandTimeoutSeconds is null or > 0, "Database:CommandTimeoutSeconds must be positive when set.")
            .Validate(o => o.MaxRetryAttempts >= 1, "Database:MaxRetryAttempts must be at least 1.");

        services.AddDbContext<AccessDbContext>((sp, options) =>
        {
            if (configureDatabase is not null)
            {
                configureDatabase(options);
                return;
            }

            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is missing. " +
                    "Set ConnectionStrings:Default (user-secrets or the ConnectionStrings__Default environment variable).");

            var database = sp.GetRequiredService<IOptions<AccessDatabaseOptions>>().Value;
            AccessDbContextConfigurator.Configure(options, connectionString, database);
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        // Every permission and role template declared through AddAccessPermissions.
        services.TryAddSingleton(sp => AccessCatalog.Build(sp.GetServices<AccessCatalogContribution>()));

        // Built-in role template for tenant owners: every new tenant gets an Owner role with tenant management permissions.
        services.AddSingleton(new AccessCatalogContribution(b =>
        {
b.Add("tenants.read", "View tenants");
b.Add("tenants.settings.read", "View tenant settings");
b.Add("tenants.settings.update", "Update tenant settings");
b.Add("tenants.members.read", "View tenant members");
b.Add("tenants.members.manage", "Manage tenant members");
            b.AddRoleTemplate("Owner", "Tenant owner with full tenant management access",
                "tenants.read", "tenants.settings.read", "tenants.settings.update", "tenants.members.read", "tenants.members.manage");
        }));

        // R8: composable authority policy.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAuthorityPolicyContributor, BuiltInAuthorityPolicyContributor>());
        services.TryAddSingleton<IAuthorityPolicy, DefaultAuthorityPolicy>();

        // C1: fail fast if the catalog is malformed.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, AccessCatalogStartupCheck>());

        // Persistence
        services.AddScoped<IAccessUnitOfWork, AccessUnitOfWork>();
        services.AddScoped<IAccessRoleRepository, AccessRoleRepository>();
        services.AddScoped<IAccessAssignmentRepository, AccessAssignmentRepository>();
        services.AddScoped<IAccessAuditRepository, AccessAuditRepository>();

        // Services
        services.AddSingleton<IAccessCache, AccessCache>();
        services.AddScoped<IAccessAuditRecorder, AccessAuditRecorder>();
        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<IAccessMembership, AccessMembership>();
        services.AddScoped<AccessGuard>();
        services.AddScoped<IAccessRoleService, AccessRoleService>();
        services.AddScoped<IAccessAssignmentService, AccessAssignmentService>();
        services.AddScoped<IAccessMaintenance, AccessMaintenance>();
        services.AddScoped<IAccessBootstrap, AccessBootstrap>();
        services.AddScoped<IAccessAuditLog, AccessAuditLog>();

        // Access's own view of the current user: reads from the HttpContext claims (Identity issues "sub").
        // Identity may register its own IAccessCurrentUser implementation that overrides this default.
        services.TryAddScoped<IAccessCurrentUser, ClaimsCurrentUser>();

        // Default scope context: reads the validated HTTP tenant scope item. Without a valid scope, scoped calls are refused.
        services.TryAddScoped<IAccessScopeContext, ClaimsAccessScopeContext>();

        // Platform membership management: add, revoke, is-member.
        services.AddScoped<IPlatformMembershipService, PlatformMembershipService>();

        // What Access offers other modules, through Access's own abstractions.
        services.AddScoped<IPermissionChecker, AccessPermissionChecker>();

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
