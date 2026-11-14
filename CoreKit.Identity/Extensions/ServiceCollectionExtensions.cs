using CoreKit.Access.Extensions;
using CoreKit.Identity.Repositories;

namespace CoreKit.Identity.Extensions;

public static class ServiceCollectionExtensions
{
    public const string ConnectionStringName = "Identity";

    /// <summary>
    /// The smallest registration: the Identity database, a clock and the password hasher.
    /// Tools such as the seeder use this and nothing else.
    /// </summary>
    public static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDatabase = null)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(o => o.MaxPoolSize is null or > 0, "Database:MaxPoolSize must be positive when set.")
            .Validate(o => o.CommandTimeoutSeconds is null or > 0, "Database:CommandTimeoutSeconds must be positive when set.")
            .Validate(o => o.MaxRetryAttempts >= 1, "Database:MaxRetryAttempts must be at least 1.");

        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            if (configureDatabase is not null)
            {
                configureDatabase(options);
                return;
            }

            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is missing. " +
                    "Set ConnectionStrings:Identity (user-secrets or the ConnectionStrings__Identity environment variable).");

            var database = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            IdentityDbContextConfigurator.Configure(options, connectionString, database);
        });

        // TryAdd: a host or a test may register its own clock or hasher first.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPasswordHasher>(_ => new PasswordHasher());

        return services;
    }

    /// <summary>
    /// Registers all of Identity for a web app: database, repositories, services, JWT authentication,
    /// permission-based authorization and rate limiting.
    /// Reads "ConnectionStrings:Identity", "Identity:Jwt" and "Identity:RateLimit" from configuration.
    /// </summary>
    /// <param name="configureDatabase">
    /// Optional. Replaces the PostgreSQL setup, which is how tests plug in an in-memory database.
    /// </param>
    public static IServiceCollection AddIdentity(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDatabase = null)
    {
        services.AddIdentityPersistence(configuration, configureDatabase);

        // Identity's own permissions are declared in the Access catalog. Access owns the catalog and
        // the authorization decision; Identity only names what it needs. Contributions are lazy and
        // harmless when AddAccess is not called.
        services.AddAccessPermissions(p =>
        {
            foreach (var (name, description) in IdentityAccessPermissionNames.All)
                p.Add(name, description);
        });

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
                "Identity:Jwt:SigningKey must be set and at least 32 bytes long.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Identity:Jwt:Issuer and Identity:Jwt:Audience must be set.")
            .Validate(o => o.AccessTokenMinutes is >= 1 and <= 120, "Identity:Jwt:AccessTokenMinutes must be 1 to 120.")
            .Validate(o => o.RefreshTokenDays is >= 1 and <= 365, "Identity:Jwt:RefreshTokenDays must be 1 to 365.")
            .ValidateOnStart(); // a missing secret stops the app at startup, not on the first login

        services.AddOptions<IdentityRateLimitOptions>()
            .Bind(configuration.GetSection(IdentityRateLimitOptions.SectionName))
            .Validate(o => o.PermitLimit >= 1 && o.WindowSeconds >= 1, "Identity:RateLimit values must be positive.");

        services.AddOptions<IdentityEmailVerificationOptions>()
            .Bind(configuration.GetSection(IdentityEmailVerificationOptions.SectionName))
            .Validate(o => o.TokenHours is >= 1 and <= 168 && o.ResendCooldownSeconds >= 0,
                "Identity:EmailVerification values are out of range.");

        services.AddHttpContextAccessor();

        // Persistence: the only code that touches the DbContext.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();

        // Focused collaborators.
        services.AddScoped<IAdministratorGuard, AdministratorGuard>();
        services.AddScoped<ISessionIssuer, SessionIssuer>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<CoreKit.Access.Abstractions.ICurrentUser>(sp => sp.GetRequiredService<ICurrentUserService>());
        // Identity's current-user is the host's current-user for Access too, so there is one implementation.
        services.TryAddScoped<CoreKit.Access.Abstractions.IAccessCurrentUser>(sp => sp.GetRequiredService<ICurrentUserService>());

        // Business services.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        services.TryAddSingleton<IIdentityEmailSender, NullIdentityEmailSender>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Read the settings lazily, so they come from the same validated options the token service uses.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                bearer.MapInboundClaims = false; // keep claim names exactly as issued: "sub", "role", "permission"

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(IdentityRateLimiting.AuthPolicy, httpContext =>
            {
                var settings = httpContext.RequestServices.GetRequiredService<IOptions<IdentityRateLimitOptions>>().Value;

                if (!settings.Enabled)
                    return RateLimitPartition.GetNoLimiter("identity-unlimited");

                // Behind a reverse proxy this is the proxy's address unless forwarded headers are configured.
                var client = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    QueueLimit = 0
                });
            });
        });

        return services;
    }
}