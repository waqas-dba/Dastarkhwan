using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Constants;
using CoreKit.Tenant.Persistence;

namespace CoreKit.Tenant.Extensions;

public static class TenantServiceCollectionExtensions
{
    public const string ConnectionStringName = "Default";

    /// <summary>
    /// Registers everything Tenant needs: database, repositories, services, tenant resolution.
    /// Reads "ConnectionStrings:Default" and "Tenant".
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

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(o => o.MaxPoolSize is null or > 0, "Database:MaxPoolSize must be positive when set.")
            .Validate(o => o.CommandTimeoutSeconds is null or > 0, "Database:CommandTimeoutSeconds must be positive when set.")
            .Validate(o => o.MaxRetryAttempts >= 1, "Database:MaxRetryAttempts must be at least 1.");

        services.AddDbContext<TenantDbContext>((sp, options) =>
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

            var database = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            TenantDbContextConfigurator.Configure(options, connectionString, database);

            // RLS interceptors removed per P1A plan (defective PostgreSQL command construction).
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // Persistence
        services.AddScoped<ITenantUnitOfWork, TenantUnitOfWork>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantSettingRepository, TenantSettingRepository>();
        services.AddScoped<ITenantMemberRepository, TenantMemberRepository>();
        services.AddScoped<ITenantAuditRepository, TenantAuditRepository>();
        services.AddScoped<ITenantOutbox>(sp => new TenantOutbox(
            sp.GetRequiredService<TenantDbContext>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CoreKit.Tenant.Settings.TenantOptions>>().Value
                .PersistOutboxMessages));

        // The tenant of the current request: one object, three views of it.
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<CurrentTenant>());

        // Who is acting, and the tenant boundary. TryAdd: a host without Identity can register its own actor first.
        services.TryAddScoped<ITenantActor, TenantActor>();
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
        services.AddScoped<ITenantMembershipReader, TenantMembershipReader>();
        services.AddScoped<ITenantMembershipCache>(sp => (ITenantMembershipCache)sp.GetRequiredService<ITenantMembershipReader>());

        // Resolution strategies. Other modules add theirs the same way.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITenantResolutionStrategy, ClaimTenantResolutionStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITenantResolutionStrategy, HeaderTenantResolutionStrategy>());

        return services;
    }
}