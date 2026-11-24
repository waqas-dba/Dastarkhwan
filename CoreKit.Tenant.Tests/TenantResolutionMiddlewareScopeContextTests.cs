using System.Security.Claims;
using CoreKit.Access.Contracts;
using CoreKit.Access.Extensions;
using CoreKit.Access.Services;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Resolution;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreKit.Tenant.Tests;

public sealed class TenantResolutionMiddlewareScopeContextTests
{
    [Fact]
    public async Task SetsValidatedTenantScope_OnlyAfterSuccessfulAuthenticatedResolution()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setter = new TestTenantContextSetter();
        var membership = new FakeTenantMembershipReader();
        membership.Members[(tenantId, userId)] = new TenantMemberInfo(false);
        var provider = new FakeTenantInfoProvider(new TenantInfo(tenantId, "Acme", "acme", TenantStatus.Active));
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            Options.Create(new TenantResolutionOptions()));
        var context = new DefaultHttpContext();
        context.User = Authenticated(userId);
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        var strategies = new ITenantResolutionStrategy[] { new FixedStrategy(new TenantIdentifier(tenantId, null, "header")) };

        await middleware.InvokeAsync(context, membership, strategies, provider, setter);

        Assert.True(nextCalled);
        Assert.Equal(tenantId, setter.TenantId);
        Assert.Equal(tenantId, context.Items[AccessScopeContextItemKeys.TenantId]);
    }

    [Fact]
    public async Task AnonymousRequest_WithTrustedHeaderAndValidActiveTenant_SetsTenantContextWithoutAccessScope()
    {
        var tenantId = Guid.NewGuid();
        var setter = new TestTenantContextSetter();
        var membership = new FakeTenantMembershipReader();
        var provider = new FakeTenantInfoProvider(new TenantInfo(tenantId, "Acme", "acme", TenantStatus.Active));
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            Options.Create(new TenantResolutionOptions()));
        var context = new DefaultHttpContext();
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        var strategies = new ITenantResolutionStrategy[] { new FixedStrategy(new TenantIdentifier(tenantId, null, "header")) };

        await middleware.InvokeAsync(context, membership, strategies, provider, setter);

        Assert.True(nextCalled);
        Assert.Equal(tenantId, setter.TenantId);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
    }

    [Fact]
    public async Task AnonymousRequest_CannotObtainAccessPermission_UsingRealMiddlewareAndAccessScopeContext()
    {
        var tenantId = Guid.NewGuid();
        var setter = new TestTenantContextSetter();
        var membership = new FakeTenantMembershipReader();
        var provider = new FakeTenantInfoProvider(new TenantInfo(tenantId, "Acme", "acme", TenantStatus.Active));
        var middleware = new TenantResolutionMiddleware(
            _ => Task.CompletedTask,
            Options.Create(new TenantResolutionOptions()));
        var context = new DefaultHttpContext();
        var strategies = new ITenantResolutionStrategy[] { new FixedStrategy(new TenantIdentifier(tenantId, null, "header")) };

        await middleware.InvokeAsync(context, membership, strategies, provider, setter);

        Assert.Equal(tenantId, setter.TenantId);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));

        using var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        connection.Open();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Access:MaxRolesPerScope"] = "1",
            ["Access:MaxRolesPerUser"] = "1",
            ["Access:CacheSeconds"] = "0",
            ["Access:UserIdClaimType"] = "sub",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAccess(config, db => db.UseSqlite(connection));
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context });
        using var sp = services.BuildServiceProvider(validateScopes: true);
        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<CoreKit.Access.Persistence.AccessDbContext>().Database.EnsureCreated();

        var scopeContext = scope.ServiceProvider.GetRequiredService<IAccessScopeContext>();
        Assert.IsType<ClaimsAccessScopeContext>(scopeContext);
        Assert.Null(scopeContext.Current);

        var checker = scope.ServiceProvider.GetRequiredService<CoreKit.Access.Contracts.IPermissionChecker>();
        Assert.False(await checker.HasPermissionAsync("sales.create"));
    }

    [Fact]
    public async Task ClearsPreExistingScope_AndDoesNotSet_WhenAuthenticatedMemberValidationFails()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setter = new TestTenantContextSetter();
        var membership = new FakeTenantMembershipReader();
        var provider = new FakeTenantInfoProvider(new TenantInfo(tenantId, "Acme", "acme", TenantStatus.Active));
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            Options.Create(new TenantResolutionOptions()));
        var context = new DefaultHttpContext();
        context.User = Authenticated(userId);
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        var strategies = new ITenantResolutionStrategy[] { new FixedStrategy(new TenantIdentifier(tenantId, null, "claim")) };

        await middleware.InvokeAsync(context, membership, strategies, provider, setter);

        Assert.False(nextCalled);
        Assert.Null(setter.TenantId);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ClearsPreExistingScope_AndDoesNotSet_WhenTenantIsUnknown()
    {
        var setter = new TestTenantContextSetter();
        var middleware = new TenantResolutionMiddleware(
            _ => Task.CompletedTask,
            Options.Create(new TenantResolutionOptions()));
        var context = new DefaultHttpContext();
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        var strategies = new ITenantResolutionStrategy[] { new FixedStrategy(new TenantIdentifier(Guid.NewGuid(), null, "header")) };

        await middleware.InvokeAsync(context, new FakeTenantMembershipReader(), strategies, new FakeTenantInfoProvider(), setter);

        Assert.Null(setter.TenantId);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task ClearsPreExistingScope_AndDoesNotSet_WhenTenantIsInactive()
    {
        var tenantId = Guid.NewGuid();
        var setter = new TestTenantContextSetter();
        var middleware = new TenantResolutionMiddleware(
            _ => Task.CompletedTask,
            Options.Create(new TenantResolutionOptions()));
        var context = new DefaultHttpContext();
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        var provider = new FakeTenantInfoProvider(new TenantInfo(tenantId, "Acme", "acme", TenantStatus.Suspended));
        var strategies = new ITenantResolutionStrategy[] { new FixedStrategy(new TenantIdentifier(tenantId, null, "header")) };

        await middleware.InvokeAsync(context, new FakeTenantMembershipReader(), strategies, provider, setter);

        Assert.Null(setter.TenantId);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ClearsPreExistingScope_AndDoesNotSet_WhenTenantIdentifiersMismatch()
    {
        var own = Guid.NewGuid();
        var other = Guid.NewGuid();
        var setter = new TestTenantContextSetter();
        var middleware = new TenantResolutionMiddleware(
            _ => Task.CompletedTask,
            Options.Create(new TenantResolutionOptions()));
        var context = new DefaultHttpContext();
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        var provider = new FakeTenantInfoProvider(
            new TenantInfo(own, "Own", "own", TenantStatus.Active),
            new TenantInfo(other, "Other", "other", TenantStatus.Active));
        var strategies = new ITenantResolutionStrategy[]
        {
            new FixedStrategy(new TenantIdentifier(own, null, "claim")),
            new FixedStrategy(new TenantIdentifier(other, null, "header"))
        };

        await middleware.InvokeAsync(context, new FakeTenantMembershipReader(), strategies, provider, setter);

        Assert.Null(setter.TenantId);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ClearsPreExistingScope_AndDoesNotSet_WhenAuthenticatedUserIdentityIsInvalid()
    {
        var tenantId = Guid.NewGuid();
        var setter = new TestTenantContextSetter();
        var membership = new FakeTenantMembershipReader();
        var provider = new FakeTenantInfoProvider(new TenantInfo(tenantId, "Acme", "acme", TenantStatus.Active));
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            Options.Create(new TenantResolutionOptions()));
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "not-a-guid") }, "test"));
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        var strategies = new ITenantResolutionStrategy[] { new FixedStrategy(new TenantIdentifier(tenantId, null, "claim")) };

        await middleware.InvokeAsync(context, membership, strategies, provider, setter);

        Assert.False(nextCalled);
        Assert.Null(setter.TenantId);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ClearsPreExistingScope_WhenNoTenantIsResolved()
    {
        var setter = new TestTenantContextSetter();
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            Options.Create(new TenantResolutionOptions()));
        var context = new DefaultHttpContext();
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();

        await middleware.InvokeAsync(context, new FakeTenantMembershipReader(), Array.Empty<ITenantResolutionStrategy>(), new FakeTenantInfoProvider(), setter);

        Assert.True(nextCalled);
        Assert.Null(setter.TenantId);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
    }

    private static ClaimsPrincipal Authenticated(Guid userId)
        => new(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "test"));

    private sealed class FixedStrategy : ITenantResolutionStrategy
    {
        private readonly TenantIdentifier _identifier;
        public FixedStrategy(TenantIdentifier identifier) => _identifier = identifier;
        public int Order => 0;
        public Task<TenantIdentifier?> ResolveAsync(HttpContext context, CancellationToken ct = default)
            => Task.FromResult<TenantIdentifier?>(_identifier);
    }

    private sealed class FakeTenantInfoProvider : ITenantInfoProvider
    {
        private readonly Dictionary<Guid, TenantInfo> _byId;
        public FakeTenantInfoProvider(params TenantInfo[] tenants)
            => _byId = tenants.ToDictionary(t => t.Id);
        public Task<TenantInfo?> GetByIdAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_byId.TryGetValue(tenantId, out var tenant) ? tenant : null);
        public Task<TenantInfo?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult<TenantInfo?>(null);
    }

    private sealed class TestTenantContextSetter : ITenantContextSetter
    {
        public Guid? TenantId { get; private set; }
        public void Set(Guid tenantId) => TenantId = tenantId;
    }
}
