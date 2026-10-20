using CoreKit.Access.Constants;
using CoreKit.Access.Endpoints;
using CoreKit.Access.Extensions;
using CoreKit.Access.Persistence;
using CoreKit.IAM.Authorization;
using CoreKit.IAM.Constants;
using CoreKit.IAM.Endpoints;
using CoreKit.IAM.Extensions;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Persistence;
using CoreKit.IAM.SampleHost;
using CoreKit.IAM.Seeding;
using CoreKit.SharedKernel;
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
builder.Services.AddAccess(builder.Configuration);   // reads the user, tenant and membership through the shared kernel

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
});

var app = builder.Build();

// Development convenience: create or update the databases, then seed them.
// In production, run migrations as a separate deployment step instead.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IamDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<AccessDbContext>().Database.MigrateAsync();
}

// The Administrator role receives every permission, including the tenants.* ones.
await app.Services.SeedIamAsync(TenantPermissionSeeds.All);

app.UseIam();      // authentication -> rate limiter -> authorization
app.UseTenant();   // after UseIam: it reads the tenant claim from the signed-in user
app.MapIam();      // /iam/auth/..., /iam/users, /iam/roles, /iam/permissions
app.MapTenants();  // /tenants, /tenants/current, /tenants/me, /tenants/{id}/settings, /members, /audit
app.MapAccess();   // /access/me, /access/permissions, /access/roles, /access/users/{id}/roles, /access/audit

// Demo endpoints, to see the modules guarding a host app's own routes.
app.MapGet("/", () => "IAM + Tenant + Access sample host is running.");

app.MapGet("/demo/public", () => "Anyone can read this.");

app.MapGet("/demo/needs-login", () => "You are signed in.")
   .RequireAuthorization();

app.MapGet("/demo/needs-users-read", () => "You hold the users.read permission.")
   .RequirePermission(IamPermissionNames.UsersRead);

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
}

app.Run();