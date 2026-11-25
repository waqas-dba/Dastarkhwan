using CoreKit.Access.Contracts;
using CoreKit.Access.Endpoints;
using CoreKit.Access.Extensions;
using CoreKit.Identity.Endpoints;
using CoreKit.Identity.Extensions;
using CoreKit.Identity.Contracts;
using CoreKit.Identity.Persistence;
using CoreKit.Identity.Seeding;
using CoreKit.Tenant.Contracts;
using CoreKit.Tenant.Endpoints;
using CoreKit.Tenant.Extensions;
using CoreKit.Tenant.Persistence;
using CoreKit.Tenant.Outbox;
using Dastarkhwan.Integration;
using Dastarkhwan.Integration.Endpoints;
using Dastarkhwan.Web.Configuration;
using Dastarkhwan.Web.Email;
using Dastarkhwan.Web.ErrorHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.NetworkInformation;

var builder = WebApplication.CreateBuilder(args);

// Expand environment variable placeholders
builder.Configuration.AddEnvironmentVariableExpansion();

// Admin/POS host: stricter CORS - only configured origins allowed, no wildcard
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Admin", policy =>
    {
        foreach (var origin in allowedOrigins)
            policy.WithOrigins(origin);
        policy.AllowAnyMethod().AllowAnyHeader();
        // Admin API does not allow credentials by default; enable only if explicitly configured
        if (bool.Parse(builder.Configuration["Cors:AllowCredentials"] ?? "false"))
            policy.AllowCredentials();
    });
});

// HTTPS redirection and HSTS - stricter for admin
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
});
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

// Forwarded headers: trust only configured reverse-proxy IPs/networks
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (IPAddress.TryParse(proxy, out var address))
            options.KnownProxies.Add(address);
        else if (System.Net.IPNetwork.TryParse(proxy, out var network))
            options.KnownIPNetworks.Add(network);
    }
});

builder.Services.AddHttpClient();

builder.Services.AddIdentity(builder.Configuration);

// Register Problem Details (RFC 9457).
builder.Services.AddProblemDetails();

// Wire Identity's IPlatformAdministratorProvider to Access's IAccessService
builder.Services.AddScoped<IPlatformAdministratorProvider>(sp =>
{
    var access = sp.GetRequiredService<IAccessService>();
    return new AdminPlatformAdministratorProvider(access);
});

builder.Services.AddIdentitySeeding(builder.Configuration);
builder.Services.AddTenant(builder.Configuration);
builder.Services.AddAccess(builder.Configuration);

// Register the Dastarkhwan integration (cross-module wiring)
builder.Services.AddDastarkhwanIntegration();

// Register outbox publisher for external side effects
builder.Services.AddTenantOutboxPublisher();

// Register rate limiting for ALL write endpoints (stricter than public host)
builder.Services.AddTenantRateLimiting(builder.Configuration);
builder.Services.AddAccessRateLimiting();

// Register email sender
builder.Services.AddEmailSender();

// Admin-specific permissions: platform-level user management
builder.Services.AddAccessPermissions(p =>
{
    p.Add("identity.users.read", "View users");
    p.Add("identity.users.create", "Create users");
    p.Add("identity.users.update", "Edit users");
    p.Add("identity.users.delete", "Delete users");
    p.Add("tenants.platform", "Platform administration");
});

// Add access bootstrap for trusted startup provisioning
builder.Services.AddScoped<IAccessBootstrap, CoreKit.Access.Services.AccessBootstrap>();

var app = builder.Build();

// Development: auto-migrate all databases
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CoreKit.Access.Persistence.AccessDbContext>().Database.MigrateAsync();
    }
}
else
{
    // Production: verify connectivity and pending migrations
    using (var scope = app.Services.CreateScope())
    {
        var sp = scope.ServiceProvider;
        foreach (var context in new DbContext[]
        {
            sp.GetRequiredService<IdentityDbContext>(),
            sp.GetRequiredService<TenantDbContext>(),
            sp.GetRequiredService<CoreKit.Access.Persistence.AccessDbContext>()
        })
        {
            if (!await context.Database.CanConnectAsync())
                throw new InvalidOperationException($"Database for {context.GetType().Name} is not reachable.");

            var pending = await context.Database.GetPendingMigrationsAsync();
            if (pending.Any())
                throw new InvalidOperationException($"Database for {context.GetType().Name} has {pending.Count()} pending migration(s).");
        }
    }
}

// Seed the first administrator
await app.Services.SeedIdentityAsync();

// Bootstrap the initial platform administrator (trusted deployment operation)
using (var scope = app.Services.CreateScope())
{
    var platformAccess = scope.ServiceProvider.GetRequiredService<CoreKit.Tenant.Contracts.ITenantPlatformAccess>();
    using var _ = platformAccess.Grant();

    var config = scope.ServiceProvider.GetRequiredService<IOptions<CoreKit.Identity.Seeding.IdentitySeedOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(config.AdminEmail))
    {
        var userRepo = scope.ServiceProvider.GetRequiredService<CoreKit.Identity.Interfaces.IUserRepository>();
        var normalizedEmail = CoreKit.Identity.Normalization.IdentityNormalizer.NormalizeEmail(config.AdminEmail);
        var user = await userRepo.FindByNormalizedEmailAsync(normalizedEmail, CancellationToken.None);
        if (user is not null)
        {
            var bootstrap = scope.ServiceProvider.GetRequiredService<IAccessBootstrap>();
            await bootstrap.BootstrapInitialPlatformAdminAsync(user.Id, CancellationToken.None);
        }
    }
}

app.UseForwardedHeaders();
app.UseHsts();
app.UseHttpsRedirection();
app.UsePlatformExceptionHandler();
app.UseCors("Admin");
app.UseIdentity();
app.UseTenant();

// Admin host maps all endpoints but with stricter authorization
app.MapIdentity();
app.MapTenants();
app.MapAccess();

// Admin-only management endpoints
app.MapPost("/admin/platform/members", async (
    AddPlatformMemberRequest req,
    IPermissionChecker checker,
    CancellationToken ct) =>
{
    if (!await checker.HasPlatformPermissionAsync("tenants.platform", ct))
        return Results.Forbid();
    return Results.Ok(new { message = "Platform member added", userId = req.UserId });
})
.RequireAuthorization()
.RequireAccess("tenants.platform");

app.MapGet("/admin/health/ready", async (
    IdentityDbContext identity,
    TenantDbContext tenant,
    CoreKit.Access.Persistence.AccessDbContext access,
    CancellationToken ct) =>
{
    var reachable = await identity.Database.CanConnectAsync(ct)
        && await tenant.Database.CanConnectAsync(ct)
        && await access.Database.CanConnectAsync(ct);
    return reachable ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
})
.RequireAuthorization();

app.Run();

// Admin platform administrator provider
internal sealed class AdminPlatformAdministratorProvider : IPlatformAdministratorProvider
{
    private readonly IAccessService _access;

    public AdminPlatformAdministratorProvider(IAccessService access)
    {
        _access = access;
    }

    public Task<IReadOnlyList<Guid>> GetPlatformAdministratorUserIdsAsync(CancellationToken ct = default)
    {
        var platformScope = AccessScope.ForPlatform(AccessScope.PlatformId);
        return _access.ListUserIdsWithRoleAsync(platformScope, "Administrator", ct);
    }
}

/// <summary>Request to add a platform member.</summary>
public sealed class AddPlatformMemberRequest
{
    public Guid UserId { get; set; }
}
