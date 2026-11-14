using CoreKit.Tenant.Resolution;
using Microsoft.Extensions.Caching.Memory;

namespace CoreKit.Tenant.Tests;

public sealed class TenantResolutionStrategyTests
{
    private readonly TenantOptions _config = new();

    private static ClaimsPrincipal Principal(string claimType, string value, bool authenticated = true)
        => new(new ClaimsIdentity(new[] { new Claim(claimType, value) }, authenticated ? "test" : null));

    [Fact]
    public async Task Claim_ReadsTheTenantOfASignedInUser()
    {
        var id = Guid.NewGuid();
        var context = new DefaultHttpContext { User = Principal("tenant_id", id.ToString()) };

        var result = await new ClaimTenantResolutionStrategy(Options.Create(_config)).ResolveAsync(context);

        Assert.Equal(new TenantIdentifier(id, null, "claim"), result);
    }

    [Fact]
    public async Task Claim_IgnoresAnonymousUsers_MissingClaims_AndBadValues()
    {
        var strategy = new ClaimTenantResolutionStrategy(Options.Create(_config));

        Assert.Null(await strategy.ResolveAsync(
            new DefaultHttpContext { User = Principal("tenant_id", Guid.NewGuid().ToString(), authenticated: false) }));
        Assert.Null(await strategy.ResolveAsync(new DefaultHttpContext()));
        Assert.Null(await strategy.ResolveAsync(
            new DefaultHttpContext { User = Principal("tenant_id", "not-a-guid") }));
        Assert.Null(await strategy.ResolveAsync(
            new DefaultHttpContext { User = Principal("tenant_id", Guid.Empty.ToString()) }));
    }

    [Fact]
    public async Task Claim_UsesTheConfiguredClaimType()
    {
        _config.Resolution.ClaimType = "org";
        var id = Guid.NewGuid();
        var context = new DefaultHttpContext { User = Principal("org", id.ToString()) };

        var result = await new ClaimTenantResolutionStrategy(Options.Create(_config)).ResolveAsync(context);

        Assert.Equal(id, result!.Id);
    }

    [Fact]
    public async Task Header_IsIgnoredUnlessTrusted()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();

        Assert.Null(await new HeaderTenantResolutionStrategy(Options.Create(_config)).ResolveAsync(context));
    }

    [Fact]
    public async Task Header_WhenTrusted_ReadsAnIdOrASlug()
    {
        _config.Resolution.TrustHeader = true;
        var strategy = new HeaderTenantResolutionStrategy(Options.Create(_config));
        var id = Guid.NewGuid();

        var byId = new DefaultHttpContext();
        byId.Request.Headers["X-Tenant-Id"] = $"  {id} ";
        var bySlug = new DefaultHttpContext();
        bySlug.Request.Headers["X-Tenant-Id"] = "acme";

        Assert.Equal(new TenantIdentifier(id, null, "header"), await strategy.ResolveAsync(byId));
        Assert.Equal(new TenantIdentifier(null, "acme", "header"), await strategy.ResolveAsync(bySlug));
        Assert.Null(await strategy.ResolveAsync(new DefaultHttpContext()));
    }

    [Fact]
    public async Task Header_UsesTheConfiguredName()
    {
        _config.Resolution.TrustHeader = true;
        _config.Resolution.HeaderName = "X-Org";
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Org"] = "acme";

        var result = await new HeaderTenantResolutionStrategy(Options.Create(_config)).ResolveAsync(context);

        Assert.Equal("acme", result!.Slug);
    }

    [Fact]
    public void ClaimsComeBeforeHeaders()
        => Assert.True(
            new ClaimTenantResolutionStrategy(Options.Create(_config)).Order
            < new HeaderTenantResolutionStrategy(Options.Create(_config)).Order);
}

public sealed class TenantResolutionMiddlewareTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private static DefaultHttpContext Request(Guid? claim = null, string? header = null, Guid? userId = null)
    {
        var context = new DefaultHttpContext();
        var claims = new List<Claim>();

        if (claim is { } id)
            claims.Add(new Claim(TenantClaimTypes.TenantId, id.ToString()));

        if (userId is { } uid)
            claims.Add(new Claim("sub", uid.ToString()));

        if (claims.Count > 0)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        if (header is not null)
            context.Request.Headers["X-Tenant-Id"] = header;

        return context;
    }

    private async Task<(DefaultHttpContext Context, bool NextCalled, string Body)> RunAsync(DefaultHttpContext context)
    {
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        var membership = new TenantMembershipReader(_env.Members, new MemoryCache(new MemoryCacheOptions()), Options.Create(_env.Config));

        var options = Options.Create(_env.Config);
        var strategies = new ITenantResolutionStrategy[]
        {
            new HeaderTenantResolutionStrategy(options),
            new ClaimTenantResolutionStrategy(options)
        };

        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context, membership, strategies, _env.InfoProvider, _env.CurrentTenant);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return (context, nextCalled, body);
    }

    [Fact]
    public async Task NoTenantIdentified_ContinuesWithoutOne()
    {
        var run = await RunAsync(Request());

        Assert.True(run.NextCalled);
        Assert.False(_env.CurrentTenant.IsResolved);
    }

    [Fact]
    public async Task ClaimResolvesAnActiveTenant()
    {
        var tenant = _env.AddTenant();
        var user = Guid.NewGuid();
        _env.AddMember(tenant.Id, user);

        var run = await RunAsync(Request(claim: tenant.Id, userId: user));

        Assert.True(run.NextCalled);
        Assert.Equal(tenant.Id, _env.CurrentTenant.Id);
    }

    [Fact]
    public async Task HeaderIsIgnoredByDefault_SoAClientCannotPickATenant()
    {
        var tenant = _env.AddTenant();

        var run = await RunAsync(Request(header: tenant.Id.ToString()));

        Assert.True(run.NextCalled);
        Assert.False(_env.CurrentTenant.IsResolved);
    }

    [Fact]
    public async Task TrustedHeaderResolvesByIdAndBySlug()
    {
        _env.Config.Resolution.TrustHeader = true;
        var tenant = _env.AddTenant("Acme", "acme");

        await RunAsync(Request(header: tenant.Id.ToString()));
        Assert.Equal(tenant.Id, _env.CurrentTenant.Id);

        using var other = new TestEnv();
        other.Config.Resolution.TrustHeader = true;
        var bySlugTenant = other.AddTenant("Shop", "shop");
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
        var membership = new TenantMembershipReader(other.Members, new MemoryCache(new MemoryCacheOptions()), Options.Create(other.Config));
        var options = Options.Create(other.Config);

        await middleware.InvokeAsync(
            Request(header: "SHOP"),
            membership,
            new ITenantResolutionStrategy[] { new HeaderTenantResolutionStrategy(options) },
            other.InfoProvider,
            other.CurrentTenant);

        Assert.Equal(bySlugTenant.Id, other.CurrentTenant.Id);
    }

    [Fact]
    public async Task ClaimAndHeaderForTheSameTenantAgree()
    {
        _env.Config.Resolution.TrustHeader = true;
        var tenant = _env.AddTenant();
        var user = Guid.NewGuid();
        _env.AddMember(tenant.Id, user);

        var run = await RunAsync(Request(claim: tenant.Id, header: tenant.Id.ToString(), userId: user));

        Assert.True(run.NextCalled);
        Assert.Equal(tenant.Id, _env.CurrentTenant.Id);
    }

    [Fact]
    public async Task ClaimAndHeaderForDifferentTenantsAreRejected()
    {
        _env.Config.Resolution.TrustHeader = true;
        var own = _env.AddTenant("Own", "own");
        var other = _env.AddTenant("Other", "other");
        var user = Guid.NewGuid();
        _env.AddMember(own.Id, user);

        var run = await RunAsync(Request(claim: own.Id, header: other.Id.ToString(), userId: user));

        Assert.False(run.NextCalled);
        Assert.Equal(403, run.Context.Response.StatusCode);
        Assert.Contains("tenant.mismatch", run.Body);
        Assert.False(_env.CurrentTenant.IsResolved);
    }

    [Fact]
    public async Task UnknownTenantIsRejectedWith404()
    {
        var run = await RunAsync(Request(claim: Guid.NewGuid()));

        Assert.False(run.NextCalled);
        Assert.Equal(404, run.Context.Response.StatusCode);
        Assert.Contains("tenant.not_found", run.Body);
    }

    [Theory]
    [InlineData(TenantStatus.Pending, "tenant.pending")]
    [InlineData(TenantStatus.Suspended, "tenant.suspended")]
    [InlineData(TenantStatus.Archived, "tenant.archived")]
    public async Task OnlyActiveTenantsMayServeRequests(TenantStatus status, string code)
    {
        var tenant = _env.AddTenant(status: status);

        var run = await RunAsync(Request(claim: tenant.Id));

        Assert.False(run.NextCalled);
        Assert.Equal(403, run.Context.Response.StatusCode);
        Assert.Contains(code, run.Body);
        Assert.False(_env.CurrentTenant.IsResolved);
        Assert.Equal("application/json", run.Context.Response.ContentType);
    }

    [Fact]
    public async Task SuspendingATenantBlocksItsNextRequestRightAway()
    {
        var tenant = _env.AddTenant();
        var user = Guid.NewGuid();
        _env.AddMember(tenant.Id, user);
        Assert.True((await RunAsync(Request(claim: tenant.Id, userId: user))).NextCalled);

        // The platform administrator acts in a request of their own, where no tenant is resolved.
        using (_env.CurrentTenant.Change(null))
            ResultAssert.Ok(await _env.CreateTenantService()
                .SuspendAsync(tenant.Id, new ChangeTenantStatusRequest()));

        // The tenant's next request starts fresh too.
        using var fresh = _env.CurrentTenant.Change(null);
        var run = await RunAsync(Request(claim: tenant.Id));

        Assert.False(run.NextCalled);
        Assert.Contains("tenant.suspended", run.Body);
    }

    [Fact]
    public async Task ActiveMember_WithValidToken_CanAccessTenant()
    {
        var tenant = _env.AddTenant();
        var user = Guid.NewGuid();
        _env.AddMember(tenant.Id, user);

        var run = await RunAsync(Request(claim: tenant.Id, userId: user));

        Assert.True(run.NextCalled);
        Assert.Equal(tenant.Id, _env.CurrentTenant.Id);
    }

    [Fact]
    public async Task RemovedMember_WithValidToken_IsRejected()
    {
        var tenant = _env.AddTenant();
        var user = Guid.NewGuid();

        // Add then remove the member
        _env.AddMember(tenant.Id, user);
        _env.Seed(db => db.Members.Remove(
            db.Members.FirstOrDefault(m => m.TenantId == tenant.Id && m.UserId == user)!));

        var run = await RunAsync(Request(claim: tenant.Id, userId: user));

        Assert.False(run.NextCalled);
        Assert.Equal(403, run.Context.Response.StatusCode);
        Assert.Contains("You are not a member of this tenant", run.Body);
        Assert.False(_env.CurrentTenant.IsResolved);
    }

    [Fact]
    public async Task NonMember_WithValidToken_IsRejected()
    {
        var tenant = _env.AddTenant();
        var user = Guid.NewGuid(); // Never added as a member

        var run = await RunAsync(Request(claim: tenant.Id, userId: user));

        Assert.False(run.NextCalled);
        Assert.Equal(403, run.Context.Response.StatusCode);
        Assert.Contains("You are not a member of this tenant", run.Body);
        Assert.False(_env.CurrentTenant.IsResolved);
    }

    [Fact]
    public async Task AuthenticatedUser_WithoutUserId_IsRejected()
    {
        var tenant = _env.AddTenant();

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(TenantClaimTypes.TenantId, tenant.Id.ToString()) },
            "test"));

        var run = await RunAsync(context);

        Assert.False(run.NextCalled);
        Assert.Equal(403, run.Context.Response.StatusCode);
        Assert.False(_env.CurrentTenant.IsResolved);
    }

    [Fact]
    public async Task AuthenticatedUser_WithMalformedUserId_IsRejected()
    {
        var tenant = _env.AddTenant();

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(TenantClaimTypes.TenantId, tenant.Id.ToString()),
                new Claim("sub", "not-a-guid")
            }, "test"));

        var run = await RunAsync(context);

        Assert.False(run.NextCalled);
        Assert.Equal(403, run.Context.Response.StatusCode);
        Assert.False(_env.CurrentTenant.IsResolved);
    }

    [Fact]
    public async Task AuthenticatedUser_WithEmptyGuidUserId_IsRejected()
    {
        var tenant = _env.AddTenant();

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(TenantClaimTypes.TenantId, tenant.Id.ToString()),
                new Claim("sub", Guid.Empty.ToString())
            }, "test"));

        var run = await RunAsync(context);

        Assert.False(run.NextCalled);
        Assert.Equal(403, run.Context.Response.StatusCode);
        Assert.False(_env.CurrentTenant.IsResolved);
    }

    [Fact]
    public async Task AuthenticatedUser_WithValidNameIdentifierClaim_Proceeds()
    {
        var tenant = _env.AddTenant();
        var userId = Guid.NewGuid();
        _env.AddMember(tenant.Id, userId);

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(TenantClaimTypes.TenantId, tenant.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            }, "test"));

        var run = await RunAsync(context);

        Assert.True(run.NextCalled);
        Assert.Equal(tenant.Id, _env.CurrentTenant.Id);
    }

    [Fact]
    public async Task AuthenticatedUser_WithMalformedNameIdentifierClaim_IsRejected()
    {
        var tenant = _env.AddTenant();

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(TenantClaimTypes.TenantId, tenant.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
            }, "test"));

        var run = await RunAsync(context);

        Assert.False(run.NextCalled);
        Assert.Equal(403, run.Context.Response.StatusCode);
        Assert.False(_env.CurrentTenant.IsResolved);
    }

    [Fact]
    public async Task AuthenticatedUser_WithEmptyGuidNameIdentifier_IsRejected()
    {
        var tenant = _env.AddTenant();

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(TenantClaimTypes.TenantId, tenant.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString())
            }, "test"));

        var run = await RunAsync(context);

        Assert.False(run.NextCalled);
        Assert.Equal(403, run.Context.Response.StatusCode);
        Assert.False(_env.CurrentTenant.IsResolved);
    }

    [Fact]
    public async Task MembershipLookupFailure_DeniesAccess()
    {
        var tenant = _env.AddTenant();
        var user = Guid.NewGuid();

        // Create a context with an authenticated user but no membership reader
        // that would return null (simulating lookup failure)
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
                new Claim(TenantClaimTypes.TenantId, tenant.Id.ToString()),
                new Claim("sub", user.ToString())
            }, "test"));

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var membership = new NullMembershipReader();

        var options = Options.Create(_env.Config);
        var strategies = new ITenantResolutionStrategy[]
        {
            new ClaimTenantResolutionStrategy(options)
        };

        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context, membership, strategies, _env.InfoProvider, _env.CurrentTenant);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("You are not a member of this tenant", body);
        Assert.False(_env.CurrentTenant.IsResolved);
    }
}

/// <summary>Returns null for every membership lookup, simulating a lookup failure.</summary>
internal sealed class NullMembershipReader : ITenantMembershipReader
{
    public Task<TenantMemberInfo?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<TenantMemberInfo?>(null);

    public Task<TenantMember?> FindForUpdateAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<TenantMember?>(null);

    public Task<IReadOnlyList<TenantMember>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TenantMember>>(new List<TenantMember>());

    public Task<IReadOnlyList<TenantMember>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TenantMember>>(new List<TenantMember>());

    public Task<IReadOnlyList<TenantMember>> ListForUserForUpdateAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TenantMember>>(new List<TenantMember>());

    public Task<int> CountOwnersAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<bool> HasDefaultAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<Guid?> FindPreferredTenantIdAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<Guid?>(null);

    public void Add(TenantMember member) => throw new NotSupportedException();

    public void Remove(TenantMember member) => throw new NotSupportedException();
}