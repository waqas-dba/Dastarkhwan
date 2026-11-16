using CoreKit.Access.Persistence;

namespace CoreKit.Access.Extensions;

public static class AccessServiceCollectionExtensions
{
    public const string ConnectionStringName = "Access";

    /// <summary>
    /// Registers Access: database, repositories, services, and defaults that read the current user and
    /// scope through Access's own abstractions. Without a scope module nobody belongs to a scope, so
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
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserIdClaimType), "Access:UserIdClaimType must be set.")
            .Validate(o => o.MaxMembershipStalenessSeconds >= 1,
                "Access:MaxMembershipStalenessSeconds must be at least 1.")
            .Validate(o => o.MembershipHeartbeatSeconds >= 1,
                "Access:MembershipHeartbeatSeconds must be at least 1.");

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
                    "Set ConnectionStrings:Access (user-secrets or the ConnectionStrings__Access environment variable).");

            var database = sp.GetRequiredService<IOptions<AccessDatabaseOptions>>().Value;
            AccessDbContextConfigurator.Configure(options, connectionString, database);
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
        services.AddScoped<IAccessMembershipProjectionRepository, AccessMembershipProjectionRepository>();
        services.AddScoped<IAccessMembershipStreamRepository, AccessMembershipStreamRepository>();
        services.AddScoped<IAccessMembershipFreshnessRepository, AccessMembershipFreshnessRepository>();
        services.AddScoped<IAccessTenantStateRepository, AccessTenantStateRepository>();

        // Services
        services.AddSingleton<IAccessCache, AccessCache>();
        services.AddScoped<IAccessAuditRecorder, AccessAuditRecorder>();
        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<AccessGuard>();
        services.AddScoped<IAccessRoleService, AccessRoleService>();
        services.AddScoped<IAccessAssignmentService, AccessAssignmentService>();
        services.AddScoped<IAccessMaintenance, AccessMaintenance>();
        services.AddScoped<IAccessAuditLog, AccessAuditLog>();

        // The projection side of tenant membership: the consumer applies Tenant's events, the installer
        // replaces a tenant's rows from a snapshot, and the reader serves Tenant's recovery and reconciliation.
        services.AddScoped<IAccessMembershipEventConsumer, AccessMembershipEventConsumer>();
        services.AddScoped<IAccessMembershipSnapshotInstaller, AccessMembershipSnapshotInstaller>();
        services.AddScoped<IAccessMembershipProjectionReader, AccessMembershipProjectionReader>();
        services.AddScoped<IAccessMembershipFreshnessRecorder, AccessMembershipFreshnessRecorder>();

        // Access's own view of the current user: reads from the HttpContext claims (Identity issues "sub").
        // Identity may register its own IAccessCurrentUser implementation that overrides this default.
        services.TryAddScoped<IAccessCurrentUser, ClaimsCurrentUser>();

        // Default scope context: reads the validated HTTP tenant scope item. Without a valid scope, scoped calls are refused.
        services.TryAddScoped<IAccessScopeContext, ClaimsAccessScopeContext>();

        // Default membership: nobody is a member, so nobody is granted anything. A membership module
        // (Tenant) may override. TryAddScoped keeps the default as a fallback.
        services.TryAddScoped<IAccessMembershipAdapter, DefaultAccessMembership>();

        // Platform membership adapter: handles platform-scoped lookups.
        services.AddScoped<IAccessMembershipAdapter, PlatformAccessMembership>();

        // Projection adapter: always registered, but it answers only for a tenant scope and only while
        // Access:MembershipSource is Projection. That keeps the cutover a configuration change, not a code change.
        // In TenantAdapter mode it returns null before it touches the database.
        services.AddScoped<IAccessMembershipAdapter, ProjectionAccessMembership>();

        // Composite router: tries each adapter in order, returns first match. Registered as the single
        // IAccessMembership so AccessService and other consumers get one implementation. TryAddScoped lets
        // a host or test register its own IAccessMembership instead.
        services.TryAddScoped<IAccessMembership, CompositeAccessMembership>();

        // Platform membership management: add, revoke, is-member.
        services.AddScoped<IPlatformMembershipService, PlatformMembershipService>();

        // What Access offers other modules, through Access's own abstractions.
        services.AddScoped<IPermissionChecker, AccessPermissionChecker>();

        // Writes the Tenant heartbeat while the projection is selected. Without it the watermark ages out and
        // every projection lookup fails closed. In TenantAdapter mode the service exits at once.
        services.AddHostedService<AccessMembershipFreshnessHeartbeatService>();

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
