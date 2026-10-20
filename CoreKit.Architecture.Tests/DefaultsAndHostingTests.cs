using CoreKit.Access.Endpoints;
using CoreKit.Access.Extensions;
using CoreKit.Architecture.Tests.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreKit.Access.Tests;

public sealed class ClaimsCurrentUserTests
{
    private static ClaimsCurrentUser For(HttpContext? context, string claimType = "sub")
        => new(
            new HttpContextAccessor { HttpContext = context },
            Options.Create(new AccessOptions { UserIdClaimType = claimType }));

    private static DefaultHttpContext SignedIn(string claimType, string value, bool authenticated = true)
        => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(claimType, value) }, authenticated ? "test" : null))
        };

    [Fact]
    public void WithoutARequest_ThereIsNoUser()
    {
        var user = For(null);

        Assert.Null(user.UserId);
        Assert.False(user.IsAuthenticated);
    }

    [Fact]
    public void AnAnonymousRequest_HasNoUser_EvenWithAClaim()
        => Assert.Null(For(SignedIn("sub", Guid.NewGuid().ToString(), authenticated: false)).UserId);

    [Fact]
    public void ASignedInRequest_ReadsTheIdFromTheClaim()
    {
        var id = Guid.NewGuid();

        var user = For(SignedIn("sub", id.ToString()));

        Assert.Equal(id, user.UserId);
        Assert.True(user.IsAuthenticated);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    public void ABadClaimValue_MeansNoUser(string value)
        => Assert.Null(For(SignedIn("sub", value)).UserId);

    [Fact]
    public void TheClaimTypeIsConfigurable()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, For(SignedIn("uid", id.ToString()), "uid").UserId);
        Assert.Null(For(SignedIn("sub", id.ToString()), "uid").UserId);
    }
}

public sealed class TenantDefaultPortsTests
{
    [Fact]
    public void ScopeContext_WithoutATenantModule_HasNoScope()
        => Assert.Null(new TenantAccessScopeContext().Current);

    [Fact]
    public void ScopeContext_WithoutAResolvedTenant_HasNoScope()
        => Assert.Null(new TenantAccessScopeContext(new FakeCurrentTenant()).Current);

    [Fact]
    public void ScopeContext_IsTheCurrentTenant()
    {
        var id = Guid.NewGuid();

        var scope = new TenantAccessScopeContext(new FakeCurrentTenant { Id = id }).Current;

        Assert.Equal(AccessScope.ForTenant(id), scope);
    }

    [Fact]
    public async Task Membership_WithoutATenantModule_NobodyBelongs()
        => Assert.Null(await new TenantAccessMembership().FindAsync(AccessScope.ForTenant(Guid.NewGuid()), Guid.NewGuid()));

    [Fact]
    public async Task Membership_ReadsTenantMembership_IncludingTheOwnerFlag()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var staff = Guid.NewGuid();
        var reader = new FakeTenantMembershipReader();
        reader.Members[(tenantId, owner)] = true;
        reader.Members[(tenantId, staff)] = false;
        var membership = new TenantAccessMembership(reader);
        var scope = AccessScope.ForTenant(tenantId);

        Assert.Equal(new AccessMember(true), await membership.FindAsync(scope, owner));
        Assert.Equal(new AccessMember(false), await membership.FindAsync(scope, staff));
        Assert.Null(await membership.FindAsync(scope, Guid.NewGuid()));
    }

    [Fact]
    public async Task Membership_IsOnlyForTenantScopes()
    {
        var id = Guid.NewGuid();
        var user = Guid.NewGuid();
        var reader = new FakeTenantMembershipReader();
        reader.Members[(id, user)] = true;

        Assert.Null(await new TenantAccessMembership(reader).FindAsync(new AccessScope("store", id), user));
    }
}

public sealed class AccessPermissionCheckerTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task AnswersForTheSignedInUserInTheCurrentScope()
    {
        _env.SignInAs("sales.create");
        var checker = new AccessPermissionChecker(_env.Access);

        Assert.True(await checker.HasPermissionAsync("sales.create"));
        Assert.False(await checker.HasPermissionAsync("sales.refund"));
    }

    [Fact]
    public async Task WithoutAUserOrAScope_TheAnswerIsNo()
    {
        var checker = new AccessPermissionChecker(_env.Access);

        Assert.False(await checker.HasPermissionAsync("sales.create"));

        _env.SignInOwner();
        _env.ScopeContext.Current = null;
        Assert.False(await checker.HasPermissionAsync("sales.create"));
    }
}

public sealed class RequireAccessAttributeTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private async Task<(ActionExecutingContext Context, bool NextCalled)> RunAsync(string permission)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAccessScopeContext>(_env.ScopeContext)
            .AddSingleton<ICurrentUser>(_env.User)
            .AddSingleton<IAccessService>(_env.Access)
            .BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(
            actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

        var nextCalled = false;

        await new RequireAccessAttribute(permission).OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        });

        return (context, nextCalled);
    }

    [Fact]
    public async Task WithoutAScope_Answers400()
    {
        _env.SignInOwner();
        _env.ScopeContext.Current = null;

        var run = await RunAsync("sales.create");

        Assert.False(run.NextCalled);
        Assert.Equal(400, Assert.IsType<ObjectResult>(run.Context.Result).StatusCode);
    }

    [Fact]
    public async Task WhenNotSignedIn_Answers401()
    {
        var run = await RunAsync("sales.create");

        Assert.False(run.NextCalled);
        Assert.Equal(401, Assert.IsType<ObjectResult>(run.Context.Result).StatusCode);
    }

    [Fact]
    public async Task WithoutThePermission_Answers403()
    {
        _env.SignInAs("products.view");

        var run = await RunAsync("sales.create");

        Assert.False(run.NextCalled);
        Assert.Equal(403, Assert.IsType<ObjectResult>(run.Context.Result).StatusCode);
    }

    [Fact]
    public async Task WithThePermission_TheActionRuns()
    {
        _env.SignInAs("sales.create");

        var run = await RunAsync("sales.create");

        Assert.True(run.NextCalled);
        Assert.Null(run.Context.Result);
    }
}

public sealed class AccessDependencyInjectionTests
{
    private static ServiceProvider Build(
        SqliteConnection connection,
        Dictionary<string, string?>? settings = null,
        Action<IServiceCollection>? before = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        before?.Invoke(services);
        services.AddAccess(configuration, db => db.UseSqlite(connection));
        services.AddAccessPermissions(p => p.Add("sales.create", "Ring up a sale"));
        services.AddAccessPermissions(p => p.Add("sales.refund", "Refund a sale")
            .AddRoleTemplate("Cashier", "Sells", "sales.create"));

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static SqliteConnection Open()
    {
        var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        connection.Open();
        return connection;
    }

    [Fact]
    public void EverythingResolves_AndTheCatalogCombinesEveryContribution()
    {
        using var connection = Open();
        using var provider = Build(connection);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IAccessService>());
        Assert.NotNull(sp.GetRequiredService<IAccessRoleService>());
        Assert.NotNull(sp.GetRequiredService<IAccessAssignmentService>());
        Assert.NotNull(sp.GetRequiredService<IAccessAuditLog>());
        Assert.NotNull(sp.GetRequiredService<IAccessMaintenance>());
        Assert.NotNull(sp.GetRequiredService<IPermissionChecker>());

        var catalog = provider.GetRequiredService<AccessCatalog>();
        Assert.True(catalog.Contains("sales.create"));
        Assert.True(catalog.Contains("sales.refund"));
        Assert.True(catalog.Contains(AccessPermissionNames.RolesManage));
        Assert.Single(catalog.RoleTemplates);
    }

    [Fact]
    public async Task OnItsOwn_NoTenantModule_NothingIsGrantedAndScopedCallsAreRefused()
    {
        using var connection = Open();
        using var provider = Build(connection);
        using var scope = provider.CreateScope();

        var access = scope.ServiceProvider.GetRequiredService<IAccessService>();

        ResultAssert.Fails(await access.GetMyAccessAsync(), AccessErrorKind.Validation, "access.scope_required");
        Assert.False(await scope.ServiceProvider.GetRequiredService<IPermissionChecker>().HasPermissionAsync("sales.create"));
    }

    [Fact]
    public async Task WithTheKernelContractsProvided_RolesWorkEndToEnd()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var reader = new FakeTenantMembershipReader();
        reader.Members[(tenantId, ownerId)] = true;

        using var connection = Open();
        using var provider = Build(connection, before: services =>
        {
            services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant { Id = tenantId });
            services.AddSingleton<ITenantMembershipReader>(reader);
            services.AddSingleton<ICurrentUser>(new FakeCurrentUser { UserId = ownerId });
        });

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AccessDbContext>().Database.EnsureCreated();

        var roles = scope.ServiceProvider.GetRequiredService<IAccessRoleService>();
        var created = ResultAssert.Ok(await roles.CreateAsync(new CreateAccessRoleRequest
        {
            Name = "Cashier",
            Permissions = { "sales.create" }
        }));

        Assert.Equal("Cashier", created.Name);
        Assert.Single(ResultAssert.Ok(await roles.ListAsync()));
        Assert.True(await scope.ServiceProvider.GetRequiredService<IPermissionChecker>().HasPermissionAsync("anything.at.all"));
    }

    [Fact]
    public void TheThreeTenantEventHandlersAreRegistered()
    {
        using var connection = Open();
        using var provider = Build(connection);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.Single(sp.GetServices<IEventHandler<TenantCreated>>());
        Assert.Single(sp.GetServices<IEventHandler<TenantMemberRemoved>>());
        Assert.Single(sp.GetServices<IEventHandler<TenantDeleted>>());
    }

    [Fact]
    public void ARegisteredUserIsNotReplacedByTheClaimsFallback()
    {
        using var connection = Open();
        var mine = new FakeCurrentUser { UserId = Guid.NewGuid() };
        using var provider = Build(connection, before: s => s.AddSingleton<ICurrentUser>(mine));
        using var scope = provider.CreateScope();

        Assert.Same(mine, scope.ServiceProvider.GetRequiredService<ICurrentUser>());
    }

    [Theory]
    [InlineData("Access:MaxRolesPerScope", "0")]
    [InlineData("Access:MaxRolesPerUser", "0")]
    [InlineData("Access:CacheSeconds", "-1")]
    [InlineData("Access:UserIdClaimType", "")]
    public void InvalidOptionsAreRejected(string key, string value)
    {
        using var connection = Open();
        using var provider = Build(connection, new Dictionary<string, string?> { [key] = value });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AccessOptions>>().Value);
    }

    [Fact]
    public void OptionsAreBoundFromConfiguration()
    {
        using var connection = Open();
        using var provider = Build(connection, new Dictionary<string, string?>
        {
            ["Access:MaxRolesPerScope"] = "7",
            ["Access:CacheSeconds"] = "5",
            ["Access:UserIdClaimType"] = "uid"
        });

        var options = provider.GetRequiredService<IOptions<AccessOptions>>().Value;

        Assert.Equal(7, options.MaxRolesPerScope);
        Assert.Equal(5, options.CacheSeconds);
        Assert.Equal("uid", options.UserIdClaimType);
    }

    [Fact]
    public void ATemplateUsingAnUndeclaredPermission_FailsWhenTheCatalogIsFirstUsed()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        using var connection = Open();
        services.AddAccess(configuration, db => db.UseSqlite(connection));
        services.AddAccessPermissions(p => p.AddRoleTemplate("Oops", null, "never.declared"));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<AccessCatalog>());
    }

    [Fact]
    public void AMissingConnectionString_FailsWithAClearMessage()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAccess(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<AccessDbContext>());

        Assert.Contains("ConnectionStrings:Access", error.Message);
    }
}