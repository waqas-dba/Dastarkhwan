using CoreKit.IAM.Authorization;
using CoreKit.IAM.Constants;
using CoreKit.IAM.Endpoints;
using CoreKit.IAM.Extensions;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Persistence;
using CoreKit.IAM.SampleHost;
using CoreKit.IAM.Seeding;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Endpoints;
using CoreKit.Tenant.Extensions;
using CoreKit.Tenant.Integration;
using CoreKit.Tenant.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Development only: prints the verification token and keeps it for the Postman tests.
// In a real app, register a sender that emails the link.
builder.Services.AddSingleton<DevEmailInbox>();
builder.Services.AddSingleton<IIamEmailSender, ConsoleEmailSender>();

builder.Services.AddIam(builder.Configuration);
builder.Services.AddIamSeeding(builder.Configuration);
builder.Services.AddTenant(builder.Configuration);   // after AddIam: it plugs into IAM's token claims

var app = builder.Build();

// Development convenience: create or update the databases, then seed them.
// In production, run migrations as a separate deployment step instead.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IamDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.MigrateAsync();
}

// The Administrator role receives every permission, including the tenants.* ones.
await app.Services.SeedIamAsync(TenantPermissionSeeds.All);

app.UseIam();      // authentication -> rate limiter -> authorization
app.UseTenant();   // after UseIam: it reads the tenant claim from the signed-in user
app.MapIam();      // /iam/auth/..., /iam/users, /iam/roles, /iam/permissions
app.MapTenants();  // /tenants, /tenants/current, /tenants/me, /tenants/{id}/settings, /members, /audit

// Demo endpoints, to see the modules guarding a host app's own routes.
app.MapGet("/", () => "IAM + Tenant sample host is running.");

app.MapGet("/demo/public", () => "Anyone can read this.");

app.MapGet("/demo/needs-login", () => "You are signed in.")
   .RequireAuthorization();

app.MapGet("/demo/needs-users-read", () => "You hold the users.read permission.")
   .RequirePermission(IamPermissionNames.UsersRead);

// Works only when the request belongs to a tenant. A suspended tenant never gets this far.
app.MapGet("/demo/needs-tenant", (ICurrentTenant tenant) => $"You are acting inside tenant {tenant.RequiredId}.")
   .RequireAuthorization()
   .RequireTenant();

// DEVELOPMENT ONLY: lets the Postman collection read the token that was "emailed".
if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/verification-token", (string email, DevEmailInbox inbox) =>
        inbox.Get(email) is { } token ? Results.Ok(new { token }) : Results.NotFound());
}

app.Run();