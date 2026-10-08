using CoreKit.IAM.Hooks;
using CoreKit.Tenant.Integration;

namespace CoreKit.Tenant.Extensions;

public static class TenantServiceCollectionExtensions
{
    public const string ConnectionStringName = "Tenant";

    /// <summary>
    /// Registers everything Tenant needs: database, repositories, services, tenant resolution and the
    /// IAM integration (a tenant claim in the access token). Reads "ConnectionStrings:Tenant" and "Tenant".
    /// Call after AddIam.
    /// </summary>
    /// <param name="configureDatabase">
    /// Optional. Replaces the PostgreSQL setup, which is how tests plug in another database.
    /// </param>
    public static IServiceCollection AddTenant(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDatabase = null)
    {
        services.AddOptions<TenantOptions>()
            .Bind(configuration.GetSection(TenantOptions.SectionName))
            .Validate(o => o.MaxSettingsPerTenant >= 1, "Tenant:MaxSettingsPerTenant must be at least 1.")
            .Validate(o => o.InfoCacheSeconds >= 0, "Tenant:InfoCacheSeconds cannot be negative.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Resolution.HeaderName) && !string.IsNullOrWhiteSpace(o.Resolution.ClaimType),
                "Tenant:Resolution:HeaderName and Tenant:Resolution:ClaimType must be set.");

        services.AddDbContext<TenantDbContext>(options =>
        {
            if (configureDatabase is not null)
            {
                configureDatabase(options);
                return;
            }

            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is missing. " +
                    "Set ConnectionStrings:Tenant (user-secrets or the ConnectionStrings__Tenant environment variable).");

            TenantDbContextConfigurator.Configure(options, connectionString);
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // Persistence
        services.AddScoped<ITenantUnitOfWork, TenantUnitOfWork>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantSettingRepository, TenantSettingRepository>();
        services.AddScoped<ITenantMemberRepository, TenantMemberRepository>();
        services.AddScoped<ITenantAuditRepository, TenantAuditRepository>();

        // The tenant of the current request: one object, three views of it.
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<CurrentTenant>());

        // Who is acting, and the tenant boundary. TryAdd: a host without IAM can register its own actor first.
        services.TryAddScoped<ITenantActor, IamTenantActor>();
        services.AddScoped<ITenantPlatformAccess, TenantPlatformAccess>();
        services.AddScoped<TenantAccessGuard>();

        // Auditing, events and the cached tenant lookup
        services.AddScoped<ITenantAuditRecorder, TenantAuditRecorder>();
        services.AddScoped<ITenantEventPublisher, TenantEventPublisher>();
        services.AddScoped<TenantInfoProvider>();
        services.AddScoped<ITenantInfoProvider>(sp => sp.GetRequiredService<TenantInfoProvider>());
        services.AddScoped<ITenantInfoCache>(sp => sp.GetRequiredService<TenantInfoProvider>());

        // Business services
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantSettingsService, TenantSettingsService>();
        services.AddScoped<ITenantMembershipService, TenantMembershipService>();
        services.AddScoped<ITenantAuditLog, TenantAuditLog>();

        // Resolution strategies. Other modules add theirs the same way.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITenantResolutionStrategy, ClaimTenantResolutionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITenantResolutionStrategy, HeaderTenantResolutionStrategy>());

        // IAM puts the tenant claim in every access token it issues.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAccessTokenClaimsContributor, TenantClaimsContributor>());

        return services;
    }
}