using System.Text;
using CoreKit.Access.Endpoints;
using CoreKit.Access.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace CoreKit.Access.Tests;

/// <summary>
/// The endpoint-authorization filter. Every case drives the real filter with a real HttpContext, and the
/// allowed and denied cases run against the real AccessService on an in-memory database.
/// </summary>
public sealed class RequireAccessFilterTests
{
    private const string Permission = "sales.create";

    [Fact]
    public async Task WithoutAScope_TheRequestIsRejected_AndTheHandlerNeverRuns()
    {
        using var env = new TestEnv();
        env.ScopeContext.Current = null;
        env.SignInAs(Permission);

        var (status, body, continued) = await Run(env);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("access.scope_required", body);
        Assert.False(continued);
    }

    [Fact]
    public async Task WithoutASignedInUser_TheRequestIsRejected_AndTheHandlerNeverRuns()
    {
        using var env = new TestEnv();
        env.User.UserId = null;

        var (status, body, continued) = await Run(env);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.Contains("access.not_authenticated", body);
        Assert.False(continued);
    }

    [Fact]
    public async Task WithoutThePermission_TheRequestIsRejected_AndTheHandlerNeverRuns()
    {
        using var env = new TestEnv();
        env.SignInAs("sales.refund");

        var (status, body, continued) = await Run(env);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Contains("access.denied", body);
        Assert.False(continued);
    }

    [Fact]
    public async Task WithThePermission_TheHandlerRuns_AndItsResultIsReturned()
    {
        using var env = new TestEnv();
        env.SignInAs(Permission);

        var (status, _, continued) = await Run(env);

        Assert.True(continued);
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task AnOwnerHoldsEveryPermission()
    {
        using var env = new TestEnv();
        env.SignInOwner();

        var (status, _, continued) = await Run(env);

        Assert.True(continued);
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task TheRequestsCancellationTokenReachesThePermissionCheck()
    {
        using var cts = new CancellationTokenSource();
        var access = new RecordingAccessService { Answer = true };
        var filter = new RequireAccessFilter(
            Permission,
            new FakeScopeContext { Current = AccessScope.ForTenant(Guid.NewGuid()) },
            new FakeCurrentUser { UserId = Guid.NewGuid() },
            access);

        var http = new DefaultHttpContext { RequestServices = EmptyProvider };
        http.RequestAborted = cts.Token;

        await filter.InvokeAsync(new TestInvocationContext(http), Next);

        Assert.Equal(1, access.Calls);
        Assert.Equal(cts.Token, access.LastToken);
    }

    [Fact]
    public async Task TheNamedPermissionIsTheOneChecked()
    {
        var access = new RecordingAccessService { Answer = false };
        var filter = new RequireAccessFilter(
            "products.manage",
            new FakeScopeContext { Current = AccessScope.ForTenant(Guid.NewGuid()) },
            new FakeCurrentUser { UserId = Guid.NewGuid() },
            access);

        var http = new DefaultHttpContext { RequestServices = EmptyProvider };

        await filter.InvokeAsync(new TestInvocationContext(http), Next);

        Assert.Equal("products.manage", access.LastPermission);
    }

    [Fact]
    public async Task TheExtensionWiresAFilterFactory_ThatResolvesTheFilterFromTheRequestScope()
    {
        using var env = new TestEnv();
        env.SignInAs(Permission);

        var builder = new RecordingConventionBuilder();
        AccessEndpointFilterExtensions.RequireAccess(builder, Permission);

        // RequireAccess must add its filter through the endpoint conventions, exactly like the
        // framework's AddEndpointFilter<TFilter>, so nothing is resolved at mapping time.
        var endpoint = new RouteEndpointBuilder(
            _ => Task.CompletedTask, RoutePatternFactory.Parse("/sales"), 0);

        Assert.Single(builder.Conventions);
        builder.Conventions[0](endpoint);

        Assert.Single(endpoint.FilterFactories);

        using var provider = BuildProvider(env);
        using var scope = provider.CreateScope();

        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        // The factory ignores its EndpointFilterFactoryContext and builds the filter per request
        // from HttpContext.RequestServices, so the filter must not come from the root provider.
        var perRequest = endpoint.FilterFactories[0](null!, Next);
        var result = (IResult)(await perRequest(new TestInvocationContext(http)))!;

        http.Response.Body = new MemoryStream();
        await result.ExecuteAsync(http);

        Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode);
    }

    /// <summary>Captures the conventions RequireAccess registers, without building a host.</summary>
    private sealed class RecordingConventionBuilder : IEndpointConventionBuilder
    {
        public List<Action<EndpointBuilder>> Conventions { get; } = new();

        public void Add(Action<EndpointBuilder> convention) => Conventions.Add(convention);
    }

    private static EndpointFilterDelegate Next { get; } = _ =>
        new ValueTask<object?>(Results.Ok("handler"));

    /// <summary>Enough for Results.Json to execute; the filter itself resolves nothing from it.</summary>
    private static IServiceProvider EmptyProvider { get; } = new ServiceCollection()
        .AddLogging()
        .AddOptions()
        .BuildServiceProvider();

    private static ServiceProvider BuildProvider(TestEnv env)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IAccessScopeContext>(_ => env.ScopeContext);
        services.AddScoped<IAccessCurrentUser>(_ => env.User);
        services.AddScoped<IAccessService>(_ => env.Access);

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task<(int Status, string Body, bool Continued)> Run(TestEnv env)
    {
        var filter = new RequireAccessFilter(Permission, env.ScopeContext, env.User, env.Access);
        var http = new DefaultHttpContext { RequestServices = EmptyProvider };

        var continued = false;
        EndpointFilterDelegate next = _ =>
        {
            continued = true;
            return new ValueTask<object?>(Results.Ok("handler"));
        };

        var result = (IResult)(await filter.InvokeAsync(new TestInvocationContext(http), next))!;

        http.Response.Body = new MemoryStream();
        await result!.ExecuteAsync(http);
        http.Response.Body.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(http.Response.Body, Encoding.UTF8);

        return (http.Response.StatusCode, await reader.ReadToEndAsync(), continued);
    }

    /// <summary>
    /// The framework's invocation context is abstract, so the test supplies the smallest concrete one.
    /// </summary>
    private sealed class TestInvocationContext : EndpointFilterInvocationContext
    {
        public TestInvocationContext(HttpContext httpContext)
        {
            HttpContext = httpContext;
        }

        public override HttpContext HttpContext { get; }

        public override IList<object?> Arguments { get; } = new List<object?>();

        public override T GetArgument<T>(int index)
            => (T)Arguments[index]!;
    }

    /// <summary>Records what the filter asked Access, so the token and permission can be asserted.</summary>
    private sealed class RecordingAccessService : IAccessService
    {
        public bool Answer { get; set; }

        public int Calls { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public string? LastPermission { get; private set; }

        public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
        {
            Calls++;
            LastPermission = permission;
            LastToken = ct;
            return Task.FromResult(Answer);
        }

        public Task<EffectiveAccess> GetAccessAsync(Guid userId, AccessScope scope, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> HasPermissionAsync(Guid userId, AccessScope scope, string permission, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<AccessResult<EffectiveAccess>> GetMyAccessAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> ListUserIdsWithRoleAsync(AccessScope scope, string roleName, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AccessRoleDto>> ListRolesAsync(AccessScope scope, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}