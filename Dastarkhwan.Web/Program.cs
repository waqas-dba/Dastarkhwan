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
using Dastarkhwan.Web.ErrorHandling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Development only: prints the verification token and keeps it for the Postman tests.
// In a real app, register a sender that emails the link.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<DevEmailInbox>();
    builder.Services.AddSingleton<IIdentityEmailSender, ConsoleEmailSender>();
}
else if (!builder.Services.Any(d => d.ServiceType == typeof(IIdentityEmailSender)))
{
    throw new InvalidOperationException(
        "A production IIdentityEmailSender must be registered before starting in non-Development environments.");
}

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

// Register the Dastarkhwan integration (cross-module wiring)
builder.Services.AddDastarkhwanIntegration();

// Register outbox publisher and projection recovery (opt-in, required for projection mode)
builder.Services.AddTenantOutboxPublisher();
builder.Services.AddTenantProjectionRecovery();

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
}

app.UsePlatformExceptionHandler();   // centralized RFC 9457 Problem Details for all exceptions
app.UseIdentity();      // authentication -> rate limiter -> authorization
app.UseTenant();   // after UseIdentity: it reads the tenant claim from the signed-in user
app.MapIdentity();      // /identity/auth/..., /identity/users, /identity/roles, /identity/permissions
app.MapTenants();  // /tenants, /tenants/current, /tenants/me, /tenants/{id}/settings, /members, /audit
app.MapAccess();   // /access/me, /access/permissions, /access/roles, /access/users/{id}/roles, /access/audit

// Demo endpoints, to see the modules guarding a host app's own routes.
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

/// <summary>DEVELOPMENT ONLY. Remembers the latest verification and reset tokens per email so tests can read them.</summary>
public sealed class DevEmailInbox
{
    private readonly ConcurrentDictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _resetTokens = new(StringComparer.OrdinalIgnoreCase);

    public void Store(string email, string token) => _tokens[email] = token;

    public string? Get(string email) => _tokens.TryGetValue(email, out var token) ? token : null;

    public void StoreReset(string email, string token) => _resetTokens[email] = token;

    public string? GetReset(string email) => _resetTokens.TryGetValue(email, out var token) ? token : null;
}

/// <summary>DEVELOPMENT ONLY. Prints the tokens instead of mailing them. Never log tokens in production.</summary>
public sealed class ConsoleEmailSender : IIdentityEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;
    private readonly DevEmailInbox _inbox;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger, DevEmailInbox inbox)
    {
        _logger = logger;
        _inbox = inbox;
    }

    public Task SendEmailVerificationAsync(EmailVerificationMessage message, CancellationToken ct = default)
    {
        _inbox.Store(message.Email, message.Token);

        _logger.LogWarning(
            "DEV ONLY: verification token for {Email} is {Token} (expires {ExpiresAt:u})",
            message.Email, message.Token, message.ExpiresAt);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(PasswordResetMessage message, CancellationToken ct = default)
    {
        _inbox.StoreReset(message.Email, message.Token);

        _logger.LogWarning(
            "DEV ONLY: password reset token for {Email} is {Token} (expires {ExpiresAt:u})",
            message.Email, message.Token, message.ExpiresAt);

        return Task.CompletedTask;
    }
}
