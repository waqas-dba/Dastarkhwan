using CoreKit.Tenant.Resolution;

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

    private static DefaultHttpContext Request(Guid? claim = null, string? header = null)
    {
        var context = new DefaultHttpContext();

        if (claim is { } id)
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim(TenantClaimTypes.TenantId, id.ToString()) }, "test"));

        if (header is not null)
            context.Request.Headers["X-Tenant-Id"] = header;

        return context;
    }

    private async Task<(DefaultHttpContext Context, bool NextCalled, string Body)> RunAsync(DefaultHttpContext context)
    {
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var options = Options.Create(_env.Config);
        var strategies = new ITenantResolutionStrategy[]
        {
            new HeaderTenantResolutionStrategy(options),
            new ClaimTenantResolutionStrategy(options)
        };

        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context, strategies, _env.InfoProvider, _env.CurrentTenant);

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

        var run = await RunAsync(Request(claim: tenant.Id));

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
        var options = Options.Create(other.Config);

        await middleware.InvokeAsync(
            Request(header: "SHOP"),
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

        var run = await RunAsync(Request(claim: tenant.Id, header: tenant.Id.ToString()));

        Assert.True(run.NextCalled);
        Assert.Equal(tenant.Id, _env.CurrentTenant.Id);
    }

    [Fact]
    public async Task ClaimAndHeaderForDifferentTenantsAreRejected()
    {
        _env.Config.Resolution.TrustHeader = true;
        var own = _env.AddTenant("Own", "own");
        var other = _env.AddTenant("Other", "other");

        var run = await RunAsync(Request(claim: own.Id, header: other.Id.ToString()));

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
        Assert.True((await RunAsync(Request(claim: tenant.Id))).NextCalled);

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
}