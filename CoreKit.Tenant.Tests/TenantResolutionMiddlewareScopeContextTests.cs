using System.Security.Claims;
using CoreKit.Access.Abstractions;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Resolution;
using Microsoft.AspNetCore.Http;

namespace CoreKit.Tenant.Tests;

public sealed class TenantResolutionMiddlewareScopeContextTests
{
    [Fact]
    public async Task SetsValidatedTenantScope_OnlyAfterSuccessfulResolution()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setter = new TestTenantContextSetter();
        var membership = new FakeTenantMembershipReader();
        membership.Members[(tenantId, userId)] = new TenantMemberInfo(false);
        var provider = new FakeTenantInfoProvider(new TenantInfo(tenantId, "Acme", "acme", TenantStatus.Active));
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.User = Authenticated(userId);
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        var strategies = new ITenantResolutionStrategy[] { new FixedStrategy(new TenantIdentifier(tenantId, null, "test")) };

        await middleware.InvokeAsync(context, membership, strategies, provider, setter);

        Assert.True(nextCalled);
        Assert.Equal(tenantId, setter.TenantId);
        Assert.Equal(tenantId, context.Items[AccessScopeContextItemKeys.TenantId]);
    }

    [Fact]
    public async Task ClearsPreExistingScope_AndDoesNotSet_WhenMembershipValidationFails()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setter = new TestTenantContextSetter();
        var membership = new FakeTenantMembershipReader();
        var provider = new FakeTenantInfoProvider(new TenantInfo(tenantId, "Acme", "acme", TenantStatus.Active));
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.User = Authenticated(userId);
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        var strategies = new ITenantResolutionStrategy[] { new FixedStrategy(new TenantIdentifier(tenantId, null, "test")) };

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
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        var strategies = new ITenantResolutionStrategy[] { new FixedStrategy(new TenantIdentifier(Guid.NewGuid(), null, "test")) };

        await middleware.InvokeAsync(context, new FakeTenantMembershipReader(), strategies, new FakeTenantInfoProvider(), setter);

        Assert.Null(setter.TenantId);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task ClearsPreExistingScope_WhenNoTenantIsResolved()
    {
        var setter = new TestTenantContextSetter();
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
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

