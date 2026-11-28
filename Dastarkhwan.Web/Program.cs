using CoreKit.Access.Constants;
using CoreKit.Access.Contracts;
using CoreKit.Identity.Interfaces;
using CoreKit.Access.Endpoints;
using CoreKit.Access.Extensions;
using CoreKit.Access.Persistence;
using CoreKit.Identity.Constants;
using CoreKit.Identity.Endpoints;
using CoreKit.Identity.Extensions;
using CoreKit.Identity.Contracts;
using CoreKit.Identity.Normalization;
using CoreKit.Identity.Persistence;
using CoreKit.Identity.Seeding;
using CoreKit.Identity.Services;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Endpoints;
using CoreKit.Tenant.Extensions;
using CoreKit.Tenant.Persistence;
using CoreKit.Identity.Outbox;
using Dastarkhwan.Integration;
using Dastarkhwan.Integration.Identity;
using Dastarkhwan.Integration.Membership;
using Dastarkhwan.Integration.Endpoints;
using Dastarkhwan.Web.Configuration;
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

builder.Configuration.AddEnvironmentVariableExpansion();

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

var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in knownProxies)
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

builder.Services.AddHttpClient();
builder.Services.AddIdentity(builder.Configuration);

builder.Services.AddProblemDetails();

builder.Services.AddScoped<IIdentityAdminAuthorizer>(sp =>
{
    var permissionChecker = sp.GetRequiredService<CoreKit.Access.Contracts.IPermissionChecker>();
    return new PermissionBasedIdentityAdminAuthorizer(permissionChecker);
});

builder.Services.AddIdentitySeeding(builder.Configuration);
builder.Services.AddHostedService<IdentityTokenRetentionService>();
builder.Services.AddIdentityOutboxRetention();
builder.Services.AddTenant(builder.Configuration);
builder.Services.AddAccess(builder.Configuration);

builder.Services.AddScoped<IPlatformAdministratorProvider>(sp =>
{
    var access = sp.GetRequiredService<IAccessService>();
    return new AccessPlatformAdministratorProvider(access);
});

builder.Services.AddDastarkhwanIntegration();
builder.Services.AddTenantRateLimiting(builder.Configuration);
builder.Services.AddAccessRateLimiting();
builder.Services.AddEmailSender();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<DevEmailInbox>();
}

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
    p.Add("identity.users.read", "View users");
    p.Add("identity.users.create", "Create users");
    p.Add("identity.users.update", "Edit users");
    p.Add("identity.users.delete", "Delete users");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment() && knownProxies.Length == 0)
{
    app.Logger.LogWarning(
        "ForwardedHeaders:KnownProxies is empty. Behind a reverse proxy every client will share the proxy's IP address, " +
        "which collapses per-IP rate limiting into a single bucket.");
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<AccessDbContext>().Database.MigrateAsync();
}
else
{
    using var scope = app.Services.CreateScope();
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

var seedOptions = builder.Configuration.GetSection("Identity:Seed").Get<IdentitySeedOptions>();
if (seedOptions != null && !string.IsNullOrWhiteSpace(seedOptions.AdminEmail))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.SeedIdentityAsync();

    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var seededUser = await userRepo.FindByNormalizedEmailAsync(
        IdentityNormalizer.NormalizeEmail(seedOptions.AdminEmail));
    if (seededUser is not null)
    {
        var bootstrap = scope.ServiceProvider.GetRequiredService<IAccessBootstrap>();
        await bootstrap.BootstrapInitialPlatformAdminAsync(seededUser.Id);
    }
}

await app.Services.ReconcileTenantRolesAsync();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

app.MapGet("/health/ready", async (
    IdentityDbContext identity,
    TenantDbContext tenant,
    AccessDbContext access,
    CancellationToken ct) =>
{
    var reachable = await identity.Database.CanConnectAsync(ct)
        && await tenant.Database.CanConnectAsync(ct)
        && await access.Database.CanConnectAsync(ct);
    return reachable ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
});

app.UseForwardedHeaders();
app.UseHsts();
app.UseHttpsRedirection();
app.UsePlatformExceptionHandler();
app.UseCors("Default");
app.UseIdentity();
app.UseTenant();

app.MapIdentity();
app.MapTenants();
app.MapAccess();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "Identity + Tenant + Access sample host is running.");
    app.MapGet("/demo/public", () => "Anyone can read this.");
    app.MapGet("/demo/needs-login", () => "You are signed in.")
        .RequireAuthorization();
    app.MapGet("/demo/needs-users-read", async (CoreKit.Access.Contracts.IPermissionChecker checker, CancellationToken ct) =>
            await checker.HasPlatformPermissionAsync(IdentityAccessPermissionNames.UsersRead, ct)
                ? Results.Ok("You hold the identity.users.read permission.")
                : Results.Forbid())
        .RequireAuthorization();
    app.MapGet("/demo/needs-tenant", (ICurrentTenant tenant) => $"You are acting inside tenant {tenant.RequiredId}.")
        .RequireAuthorization()
        .RequireTenant();
    app.MapPost("/demo/sale", () => "Sale recorded.")
        .RequireAuthorization()
        .RequireTenant()
        .RequireAccess("sales.create");
    app.MapPost("/demo/refund", () => "Refund issued.")
        .RequireAuthorization()
        .RequireTenant()
        .RequireAccess("sales.refund");
}

// Test-only endpoints that hand out account tokens: Development + explicit opt-in
// (Dev:EnableTokenEndpoints=true) + loopback callers only.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Dev:EnableTokenEndpoints"))
{
    var dev = app.MapGroup("/dev");
    dev.AddEndpointFilter(async (ctx, next) =>
    {
        if (ctx.HttpContext.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip))
            return await next(ctx);
        return Results.NotFound();
    });
    dev.MapGet("/verification-token", (string email, DevEmailInbox inbox) =>
        inbox.Get(email) is { } token ? Results.Ok(new { token }) : Results.NotFound());
    dev.MapGet("/password-reset-token", (string email, DevEmailInbox inbox) =>
        inbox.GetReset(email) is { } token ? Results.Ok(new { token }) : Results.NotFound());
}

app.Run();

