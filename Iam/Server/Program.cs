using Iam.Data;
using Iam.Data.Persistence;
using Iam.Data.Seeding;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Comes from appsettings, user-secrets or the ConnectionStrings__Iam environment variable.
var connectionString = builder.Configuration.GetConnectionString("Iam")
    ?? throw new InvalidOperationException(
        "Connection string 'Iam' is missing. Set the ConnectionStrings__Iam environment variable.");

builder.Services.AddIamData(connectionString);

var app = builder.Build();

// Development convenience only. In production, run migrations as a separate one-off step:
// if three instances start at once, they would all try to migrate the same database.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    await scope.ServiceProvider.GetRequiredService<IamDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IamSeeder>().SeedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();