using CoreKit.IAM.Authorization;
using CoreKit.IAM.Constants;
using CoreKit.IAM.Endpoints;
using CoreKit.IAM.Extensions;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Persistence;
using CoreKit.IAM.SampleHost;
using CoreKit.IAM.Seeding;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Development only: prints the verification token and keeps it for the Postman tests.
// In a real app, register a sender that emails the link.
builder.Services.AddSingleton<DevEmailInbox>();
builder.Services.AddSingleton<IIamEmailSender, ConsoleEmailSender>();

builder.Services.AddIam(builder.Configuration);
builder.Services.AddIamSeeding(builder.Configuration);

var app = builder.Build();

// Development convenience: create or update the database, then seed it.
// In production, run migrations as a separate deployment step instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IamDbContext>();
    await db.Database.MigrateAsync();
}

await app.Services.SeedIamAsync();

app.UseIam();   // authentication -> rate limiter -> authorization
app.MapIam();   // /iam/auth/..., /iam/users, /iam/roles, /iam/permissions

// Demo endpoints, to see the module guarding a host app's own routes.
app.MapGet("/", () => "IAM sample host is running.");

app.MapGet("/demo/public", () => "Anyone can read this.");

app.MapGet("/demo/needs-login", () => "You are signed in.")
   .RequireAuthorization();

app.MapGet("/demo/needs-users-read", () => "You hold the users.read permission.")
   .RequirePermission(IamPermissionNames.UsersRead);

// DEVELOPMENT ONLY: lets the Postman collection read the token that was "emailed".
if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/verification-token", (string email, DevEmailInbox inbox) =>
        inbox.Get(email) is { } token ? Results.Ok(new { token }) : Results.NotFound());
}

app.Run();