using CoreKit.Access.Abstractions;
using CoreKit.Access.Constants;
using CoreKit.Access.Endpoints;
using CoreKit.Access.Extensions;
using CoreKit.Access.Persistence;
using CoreKit.Identity.Constants;
using CoreKit.Identity.Endpoints;
using CoreKit.Identity.Extensions;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Persistence;
using CoreKit.Identity.Seeding;
using CoreKit.Identity.Services;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Endpoints;
using CoreKit.Tenant.Extensions;
using CoreKit.Tenant.Persistence;
using CoreKit.Tenant.Outbox;
using Dastarkhwan.Integration;
using Dastarkhwan.Integration.Endpoints;
using Dastarkhwan.Web.Email;
using Dastarkhwan.Web.ErrorHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// CORS: read allowed origins from configuration. Development allows any origin;
// production requires explicit configuration (empty list = no cross-origin access).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else
        {
            foreach (var origin in allowedOrigins)
                policy.WithOrigins(origin);
            policy.AllowAnyMethod().AllowAnyHeader();
        }
    });
});

// HTTPS redirection and HSTS.
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    options.HttpsPort = null;
});
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(90);
    options.IncludeSubDomains = true;
});

// Forwarded headers: trust X-Forwarded-For and X-Forwarded-Proto from a reverse proxy.
 // Do NOT clear KnownProxies/KnownNetworks: clearing them trusts any client-supplied
 // forwarded header, enabling IP spoofing and rate-limit bypass. Trusted reverse-proxy
 // addresses must be added explicitly via "ForwardedHeaders:KnownProxies" configuration.
 // By default only loopback is trusted, which is safe for non-proxied deployments.
 builder.Services.Configure<ForwardedHeadersOptions>(options =>
 {
     options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

     foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
     {
         if (System.Net.IPAddress.TryParse(proxy, out var address))
         {
             options.KnownProxies.Add(address);
         }
         else if (System.Net.IPNetwork.TryParse(proxy, out var network))
         {
             options.KnownIPNetworks.Add(network);
         }
     }
 });

// Register HttpClient for SaaS email providers (SendGrid, Mailgun)
builder.Services.AddHttpClient();

builder.Services.AddIdentity(builder.Configuration);

// Register Problem Details (RFC 9457).
builder.Services.AddProblemDetails();

// The platform exception middleware is registered in the pipeline below.
// Here we map identity.* operations to platform permissions checked through Access.
builder.Services.AddScoped<IIdentityAdminAuthorizer>(sp =>
{
    var permissionChecker = sp.GetRequiredService<CoreKit.Access.Abstractions.IPermissionChecker>();
    return new PermissionBasedIdentityAdminAuthorizer(permissionChecker);
});
builder.Services.AddIdentitySeeding(builder.Configuration);
builder.Services.AddTenant(builder.Configuration);
builder.Services.AddAccess(builder.Configuration);

// Wire Identity's IPlatformAdministratorProvider to Access's IAccessService
 // This keeps Identity independent of Access; the host (which references both) connects them.
 builder.Services.AddScoped<CoreKit.Identity.Interfaces.IPlatformAdministratorProvider>(sp =>
 {
     var access = sp.GetRequiredService<CoreKit.Access.Abstractions.IAccessService>();
     return new AccessPlatformAdministratorProvider(access);
 });

 // Register the Dastarkhwan integration (cross-module wiring)
 builder.Services.AddDastarkhwanIntegration();

// Register outbox publisher and projection recovery (opt-in, required for projection mode)
builder.Services.AddTenantOutboxPublisher();
builder.Services.AddTenantProjectionRecovery();

// Register AccessBootstrap for trusted startup provisioning
builder.Services.AddScoped<CoreKit.Access.Abstractions.IAccessBootstrap, CoreKit.Access.Services.AccessBootstrap>();

// Register rate limiting for tenant and access write endpoints
builder.Services.AddTenantRateLimiting(builder.Configuration);
builder.Services.AddAccessRateLimiting();

// Register email sender based on environment and configuration
builder.Services.AddEmailSender();

// Register DevEmailInbox for development token retrieval
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<DevEmailInbox>();
}

// The application declares what can be allowed, and the roles every new tenant starts with.
builder.Services.AddAccessPermissions(p =>
{
    p.Add("sales.create", "Ring up a sale");
    p.Add("sales.refund", "Refund a sale");
    p.Add("products.view", "See products");
    p.Add("products.manage", "Create and edit products");

    p.AddRoleTemplate(
        "Manager", "Runs the shop day to day",
        "sales.create", "sales.refund", "products.view", "products.manage",
        AccessPermissionNames.RolesRead, AccessPermissionNames.AssignmentsRead, AccessPermissionNames.AssignmentsManage);

    p.AddRoleTemplate("Cashier", "Rings up sales", "sales.create", "products.view");

    // Identity user-management permissions, checked at platform scope by the host's
    // IIdentityAdminAuthorizer implementation.
    p.Add("identity.users.read", "View users");
    p.Add("identity.users.create", "Create users");
    p.Add("identity.users.update", "Edit users");
    p.Add("identity.users.delete", "Delete users");
});

var app = builder.Build();

// Development convenience: create or update the databases, then seed them.
// In production, run migrations as a separate deployment step instead.
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AccessDbContext>().Database.MigrateAsync();
    }
}
else
    {
        // Production must never auto-migrate. Verify connectivity so an incompatible or
        // unreachable database fails safely with an actionable diagnostic.
        using (var scope = app.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            foreach (var context in new DbContext[]
            {
                sp.GetRequiredService<IdentityDbContext>(),
                sp.GetRequiredService<TenantDbContext>(),
                sp.GetRequiredService<AccessDbContext>()
            })
            {
                if (!await context.Database.CanConnectAsync())
                {
                    throw new InvalidOperationException(
                        $"Database for {context.GetType().Name} is not reachable. Apply migrations as a deployment step before starting the application.");
                }

                // Check for pending migrations in production
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    var logger = sp.GetRequiredService<ILogger<Program>>();
                    logger.LogError("Database {Context} has pending migrations: {Migrations}. Apply migrations before starting in production.",
                        context.GetType().Name, string.Join(", ", pendingMigrations));
                    throw new InvalidOperationException(
                        $"Database for {context.GetType().Name} has pending migrations: {string.Join(", ", pendingMigrations)}. Apply migrations as a deployment step before starting the application.");
                }
            }
        }
    }

// Access owns roles and permissions; the seeder only ensures the first administrator exists.
await app.Services.SeedIdentityAsync();

// Bootstrap the Access projection from Tenant's authoritative membership data.
// This runs with platform scope authorization and must complete before serving
// authorization traffic in Projection mode.
using (var scope = app.Services.CreateScope())
{
    // Grant platform access for the trusted startup recovery operation (no authenticated user yet).
    var platformAccess = scope.ServiceProvider.GetRequiredService<CoreKit.Tenant.Services.TenantPlatformAccess>();
    using var _ = platformAccess.Grant();

    var recovery = scope.ServiceProvider.GetRequiredService<TenantProjectionRecoveryService>();
    var result = await recovery.RebuildAllAsync(CancellationToken.None);
    if (result.Value.Failed.Count > 0)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Projection bootstrap failed for {FailedCount} tenants: {FailedIds}",
            result.Value.Failed.Count,
            string.Join(", ", result.Value.Failed.Select(f => f.ToString())));
    }
    else
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Projection bootstrap complete: {Installed} installed, {Repaired} repaired, {Unchanged} unchanged, {Stale} ignored",
            result.Value.Installed, result.Value.Repaired, result.Value.Unchanged, result.Value.IgnoredStale);
}

// Bootstrap the initial platform administrator (idempotent).
    // This is a trusted deployment operation; the target user is the first Identity admin.
    var bootstrap = scope.ServiceProvider.GetRequiredService<CoreKit.Access.Abstractions.IAccessBootstrap>();
    var config = scope.ServiceProvider.GetRequiredService<IOptions<CoreKit.Identity.Seeding.IdentitySeedOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(config.AdminEmail))
    {
        var userRepo = scope.ServiceProvider.GetRequiredService<CoreKit.Identity.Interfaces.IUserRepository>();
        var normalizedEmail = CoreKit.Identity.Normalization.IdentityNormalizer.NormalizeEmail(config.AdminEmail);
        var user = await userRepo.FindByNormalizedEmailAsync(normalizedEmail, CancellationToken.None);
        if (user is not null)
        {
            await bootstrap.BootstrapInitialPlatformAdminAsync(user.Id, CancellationToken.None);
        }
    }
}

app.UseForwardedHeaders();   // trust X-Forwarded-For / X-Forwarded-Proto from reverse proxy
app.UseHsts();               // Strict-Transport-Security header
app.UseHttpsRedirection();   // redirect HTTP to HTTPS
app.UsePlatformExceptionHandler();   // centralized RFC 9457 Problem Details for all exceptions
app.UseCors("Default");      // CORS before authentication
app.UseIdentity();      // authentication -> rate limiter -> authorization
app.UseTenant();   // after UseIdentity: it reads the tenant claim from the signed-in user
app.MapIdentity();      // /identity/auth/..., /identity/users, /identity/roles, /identity/permissions
app.MapTenants();  // /tenants, /tenants/current, /tenants/me, /tenants/{id}/settings, /members, /audit
app.MapAccess();   // /access/me, /access/permissions, /access/roles, /access/users/{id}/roles, /access/audit

// Demo endpoints, to see the modules guarding a host app's own routes.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Identity + Tenant + Access sample host is running.");

    app.MapGet("/demo/public", () => "Anyone can read this.");

    app.MapGet("/demo/needs-login", () => "You are signed in.")
        .RequireAuthorization();

    app.MapGet("/demo/needs-users-read", async (CoreKit.Access.Abstractions.IPermissionChecker checker, CancellationToken ct) =>
            await checker.HasPlatformPermissionAsync(IdentityAccessPermissionNames.UsersRead, ct)
                ? Results.Ok("You hold the identity.users.read permission.")
                : Results.Forbid())
        .RequireAuthorization();

    // Works only when the request belongs to a tenant. A suspended tenant never gets this far.
    app.MapGet("/demo/needs-tenant", (ICurrentTenant tenant) => $"You are acting inside tenant {tenant.RequiredId}.")
        .RequireAuthorization()
        .RequireTenant();

    // Tenant roles in action: a Cashier can sell but not refund; a Manager and the owner can do both.
    app.MapPost("/demo/sale", () => "Sale recorded.")
        .RequireAuthorization()
        .RequireTenant()
        .RequireAccess("sales.create");

    app.MapPost("/demo/refund", () => "Refund issued.")
        .RequireAuthorization()
        .RequireTenant()
        .RequireAccess("sales.refund");
}

// DEVELOPMENT ONLY: lets the Postman collection read the token that was "emailed".
if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/verification-token", (string email, DevEmailInbox inbox) =>
        inbox.Get(email) is { } token ? Results.Ok(new { token }) : Results.NotFound());

    app.MapGet("/dev/password-reset-token", (string email, DevEmailInbox inbox) =>
        inbox.GetReset(email) is { } token ? Results.Ok(new { token }) : Results.NotFound());
}

app.Run();

public sealed class PermissionBasedIdentityAdminAuthorizer : IIdentityAdminAuthorizer
{
    private readonly CoreKit.Access.Abstractions.IPermissionChecker _permissionChecker;

    public PermissionBasedIdentityAdminAuthorizer(CoreKit.Access.Abstractions.IPermissionChecker permissionChecker)
    {
        _permissionChecker = permissionChecker;
    }

    public async Task<bool> IsAllowedAsync(IdentityAdminOperation operation, CancellationToken ct = default)
    {
        var permission = operation switch
        {
            IdentityAdminOperation.ReadUsers => "identity.users.read",
            IdentityAdminOperation.CreateUsers => "identity.users.create",
            IdentityAdminOperation.UpdateUsers => "identity.users.update",
            IdentityAdminOperation.DeleteUsers => "identity.users.delete",
            _ => null
        };

        return permission is not null && await _permissionChecker.HasPlatformPermissionAsync(permission, ct);
    }
}

/// <summary>Adapter: implements Identity's IPlatformAdministratorProvider using Access's IAccessService.</summary>
internal sealed class AccessPlatformAdministratorProvider : CoreKit.Identity.Interfaces.IPlatformAdministratorProvider
{
    private readonly CoreKit.Access.Abstractions.IAccessService _access;

    public AccessPlatformAdministratorProvider(CoreKit.Access.Abstractions.IAccessService access)
    {
        _access = access;
    }

    public Task<IReadOnlyList<Guid>> GetPlatformAdministratorUserIdsAsync(CancellationToken ct = default)
    {
        var platformScope = CoreKit.Access.Abstractions.AccessScope.ForPlatform(CoreKit.Access.Abstractions.AccessScope.PlatformId);
        return _access.ListUserIdsWithRoleAsync(platformScope, "Administrator", ct);
    }
}

/// <summary>Dispatches Identity integration events to all registered IEventHandler<T> implementations.</summary>
internal sealed class DispatchingIdentityEventPublisher : CoreKit.Identity.Interfaces.IIdentityEventPublisher
{
    private readonly IServiceProvider _services;

    public DispatchingIdentityEventPublisher(IServiceProvider services)
    {
        _services = services;
    }

    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : CoreKit.Identity.Interfaces.IIdentityIntegrationEvent
    {
        foreach (var handler in _services.GetServices<CoreKit.Abstractions.IntegrationEvents.IEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(integrationEvent, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log but don't fail the originating operation
                var loggerFactory = _services.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("IdentityEventDispatch");
                logger?.LogError(ex, "Handler failed for {EventType}", typeof(TEvent).Name);
            }
        }
    }
}
