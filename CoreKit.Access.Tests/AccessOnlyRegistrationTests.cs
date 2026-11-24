using CoreKit.Access.Contracts;
using CoreKit.Access.Extensions;
using CoreKit.Access.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreKit.Access.Tests;

public sealed class AccessOnlyRegistrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public AccessOnlyRegistrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Access:MaxRolesPerScope"] = "1",
            ["Access:MaxRolesPerUser"] = "1",
            ["Access:CacheSeconds"] = "0",
            ["Access:UserIdClaimType"] = "sub",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAccess(config, db => db.UseSqlite(_connection));
        _provider = services.BuildServiceProvider(validateScopes: true);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void WithoutTenant_ResolvesAccessOwnedScopeContext_AndFailsClosed()
    {
        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<CoreKit.Access.Persistence.AccessDbContext>().Database.EnsureCreated();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();

        var scopeContext = scope.ServiceProvider.GetRequiredService<IAccessScopeContext>();
        Assert.IsType<ClaimsAccessScopeContext>(scopeContext);
        Assert.Null(scopeContext.Current);
    }
}

