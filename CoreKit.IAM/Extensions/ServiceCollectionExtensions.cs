namespace CoreKit.IAM.Extensions;

public static class ServiceCollectionExtensions
{
    public const string ConnectionStringName = "Iam";

    /// <summary>
    /// The smallest registration: the IAM database, a clock and the password hasher.
    /// Tools such as the seeder use this and nothing else.
    /// </summary>
    public static IServiceCollection AddIamPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDatabase = null)
    {
        services.AddDbContext<IamDbContext>(options =>
        {
            if (configureDatabase is not null)
            {
                configureDatabase(options);
                return;
            }

            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is missing. " +
                    "Set ConnectionStrings:Iam (user-secrets or the ConnectionStrings__Iam environment variable).");

            IamDbContextConfigurator.Configure(options, connectionString);
        });

        // TryAdd: a host or a test may register its own clock or hasher first.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPasswordHasher>(_ => new PasswordHasher());

        return services;
    }

    /// <summary>
    /// Registers all of IAM for a web app: database, services, JWT authentication,
    /// permission-based authorization and rate limiting.
    /// Reads "ConnectionStrings:Iam", "Iam:Jwt" and "Iam:RateLimit" from configuration.
    /// </summary>
    /// <param name="configureDatabase">
    /// Optional. Replaces the PostgreSQL setup, which is how tests plug in an in-memory database.
    /// </param>
    public static IServiceCollection AddDaskhawaIam(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDatabase = null)
    {
        services.AddIamPersistence(configuration, configureDatabase);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
                "Iam:Jwt:SigningKey must be set and at least 32 bytes long.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Iam:Jwt:Issuer and Iam:Jwt:Audience must be set.")
            .Validate(o => o.AccessTokenMinutes is >= 1 and <= 120, "Iam:Jwt:AccessTokenMinutes must be 1 to 120.")
            .Validate(o => o.RefreshTokenDays is >= 1 and <= 365, "Iam:Jwt:RefreshTokenDays must be 1 to 365.")
            .ValidateOnStart(); // a missing secret stops the app at startup, not on the first login

        services.AddOptions<IamRateLimitOptions>()
            .Bind(configuration.GetSection(IamRateLimitOptions.SectionName))
            .Validate(o => o.PermitLimit >= 1 && o.WindowSeconds >= 1, "Iam:RateLimit values must be positive.");

        services.AddHttpContextAccessor();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();

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
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(IamRateLimiting.AuthPolicy, httpContext =>
            {
                var settings = httpContext.RequestServices.GetRequiredService<IOptions<IamRateLimitOptions>>().Value;

                if (!settings.Enabled)
                    return RateLimitPartition.GetNoLimiter("iam-unlimited");

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