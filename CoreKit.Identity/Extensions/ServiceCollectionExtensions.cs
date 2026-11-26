using CoreKit.Identity.Constants;
using CoreKit.Identity.Contracts;
using CoreKit.Identity.Persistence;
using CoreKit.Identity.Repositories;
using CoreKit.Identity.Security;
using CoreKit.Identity.Outbox;
using CoreKit.Identity.Services;
using CoreKit.Abstractions.IntegrationEvents;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CoreKit.Identity.Extensions;

public static class ServiceCollectionExtensions
{
    public const string ConnectionStringName = "Default";

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
                    "Set ConnectionStrings:Default (user-secrets or the ConnectionStrings__Default environment variable).");

            var database = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            IdentityDbContextConfigurator.Configure(options, connectionString, database);

            // RLS interceptors removed per P1A plan (defective PostgreSQL command construction).
        });

        // TryAdd: a host or a test may register its own clock or hasher first.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPasswordHasher>(_ => new PasswordHasher());

        return services;
    }

    /// <summary>
    /// Registers all of Identity for a web app: database, repositories, services, JWT authentication,
    /// permission-based authorization and rate limiting.
    /// Reads "ConnectionStrings:Default", "Identity:Jwt" and "Identity:RateLimit" from configuration.
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

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
                "Identity:Jwt:SigningKey must be set and at least 32 bytes long.")
            .Validate(
                o => !IsPlaceholderSigningKey(o.SigningKey),
                "Identity:Jwt:SigningKey cannot be a known placeholder or weak default. Generate a cryptographically random key (32+ bytes).")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Identity:Jwt:Issuer and Identity:Jwt:Audience must be set.")
            .Validate(o => o.AccessTokenMinutes is >= 1 and <= 120, "Identity:Jwt:AccessTokenMinutes must be 1 to 120.")
            .Validate(o => o.RefreshTokenDays is >= 1 and <= 365, "Identity:Jwt:RefreshTokenDays must be 1 to 365.")
            .ValidateOnStart(); // a missing secret stops the app at startup, not on the first login

        static bool IsPlaceholderSigningKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return true;

            var normalized = key.Trim().ToLowerInvariant();
            var placeholders = new[]
            {
                "your-super-secret-signing-key-at-least-32-chars-long",
                "your-secret-key-here",
                "secret",
                "changeme",
                "changeme123",
                "supersecret",
                "supersecretkey",
                "development-secret-key-do-not-use-in-production",
                "test-secret-key",
                "placeholder",
                "default",
                "insecure",
                "weak",
                "example",
                "sample"
            };

            return placeholders.Any(p => normalized.Contains(p, StringComparison.OrdinalIgnoreCase))
                || normalized.Length < 32; // Additional safety: keys shorter than 32 chars are likely placeholders
        }

        services.AddOptions<IdentityRateLimitOptions>()
            .Bind(configuration.GetSection(IdentityRateLimitOptions.SectionName))
            .Validate(o => o.PermitLimit >= 1 && o.WindowSeconds >= 1, "Identity:RateLimit values must be positive.");

        services.AddOptions<IdentityEmailVerificationOptions>()
            .Bind(configuration.GetSection(IdentityEmailVerificationOptions.SectionName))
            .Validate(o => o.TokenHours is >= 1 and <= 168 && o.ResendCooldownSeconds >= 0,
                "Identity:EmailVerification values are out of range.");

        services.AddOptions<IdentityLockoutOptions>()
            .Bind(configuration.GetSection(IdentityLockoutOptions.SectionName))
            .Validate(o => o.Threshold is >= 1 and <= 100 && o.DurationMinutes is >= 1 and <= 1440,
                "Identity:Lockout:Threshold must be 1 to 100 and Identity:Lockout:DurationMinutes must be 1 to 1440.")
            .ValidateOnStart();

        services.AddOptions<IdentityPasswordResetOptions>()
            .Bind(configuration.GetSection(IdentityPasswordResetOptions.SectionName))
            .Validate(o => o.TokenMinutes is >= 5 and <= 1440,
                "Identity:PasswordReset:TokenMinutes must be 5 to 1440.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        // Persistence: the only code that touches the DbContext.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserLockoutRepository, UserLockoutRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();

        // Focused collaborators.
        services.AddScoped<IAdministratorGuard>(sp => new AdministratorGuard(
            sp.GetRequiredService<IPlatformAdministratorProvider>(),
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<IdentityDbContext>()));
        services.AddScoped<ISessionIssuer, SessionIssuer>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Password-reset tokens are signed with a key derived from the JWT signing key and a purpose label.
        services.AddSingleton(sp =>
            new PasswordResetTokenProtector(sp.GetRequiredService<IOptions<JwtOptions>>().Value.SigningKey));

        // Business services.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IUserService, UserService>();

        services.TryAddSingleton<IIdentityEmailSender, NullIdentityEmailSender>();

        // Outbox for durable integration events (account deletion, etc.)
        services.AddIdentityOutbox(configuration);

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

                // Validate SecurityVersion: reject tokens whose secver claim doesn't match the user's current version.
                // Uses IMemoryCache to avoid per-request DB queries. Cache TTL is short (30s) because
                // SecurityVersion changes invalidate all tokens immediately via the token family mechanism.
                bearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var secVerClaim = context.Principal?.FindFirst("secver");
                        if (secVerClaim is null || !int.TryParse(secVerClaim.Value, out var tokenSecVer))
                        {
                            context.Fail("Missing or invalid SecurityVersion claim.");
                            return;
                        }

                        var subClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub);
                        if (subClaim is null || !Guid.TryParse(subClaim.Value, out var userId))
                        {
                            context.Fail("Missing or invalid subject claim.");
                            return;
                        }

                        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                        var cacheKey = $"secver:{userId}";

                        if (!cache.TryGetValue<int>(cacheKey, out var currentSecVer))
                        {
                            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                            var user = await userRepository.FindByIdAsync(userId, context.HttpContext.RequestAborted);

                            if (user is null)
                            {
                                context.Fail("User not found.");
                                return;
                            }

                            currentSecVer = user.SecurityVersion;
                            cache.Set(cacheKey, currentSecVer, TimeSpan.FromSeconds(30));
                        }

                        if (currentSecVer != tokenSecVer)
                        {
                            context.Fail("Token SecurityVersion does not match current user SecurityVersion.");
                        }
                    }
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

internal sealed class NullIdentityEventPublisher : IIdentityEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIdentityIntegrationEvent
        => Task.CompletedTask;
}
