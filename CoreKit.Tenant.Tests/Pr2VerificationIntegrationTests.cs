using System.Security.Claims;
using CoreKit.Access.Contracts;
using CoreKit.Access.Constants;
using CoreKit.Access.Entities;
using CoreKit.Access.Extensions;
using CoreKit.Access.Persistence;
using CoreKit.Access.Services;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Extensions;
using CoreKit.Tenant.Resolution;
using Dastarkhwan.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreKit.Tenant.Tests;

public sealed class Pr2VerificationIntegrationTests : IDisposable
{
    private readonly SqliteConnection _tenantConnection;
    private readonly SqliteConnection _accessConnection;
    private readonly ServiceProvider _provider;

    public Pr2VerificationIntegrationTests()
    {
        _tenantConnection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _tenantConnection.Open();
        _accessConnection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _accessConnection.Open();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenant:MaxSettingsPerTenant"] = "10",
                ["Tenant:InfoCacheSeconds"] = "0",
                ["Tenant:MembershipCacheSeconds"] = "0",
                ["Tenant:Resolution:HeaderName"] = "X-Tenant-Id",
                ["Tenant:Resolution:TrustHeader"] = "true",
                ["Tenant:Resolution:ClaimType"] = "tenant_id",
                ["Access:MaxRolesPerScope"] = "10",
                ["Access:MaxRolesPerUser"] = "10",
                ["Access:CacheSeconds"] = "0",
                ["Access:UserIdClaimType"] = "sub",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        services.AddTenant(config, options => options.UseSqlite(_tenantConnection));
        services.AddAccess(config, options => options.UseSqlite(_accessConnection));
        services.AddDastarkhwanIntegration();

        _provider = services.BuildServiceProvider(validateScopes: true);

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<AccessDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _tenantConnection.Dispose();
        _accessConnection.Dispose();
    }

    [Fact]
    public async Task Anonymous_TrustedHeader_ValidActiveTenant_SetsCurrentTenantWithoutAccessScope_AndRequestContinues()
    {
        var tenant = await SeedTenantAsync("Acme", "acme", TenantStatus.Active);

        using var scope = _provider.CreateScope();
        var (context, nextCalled, statusCode) = await InvokeRealMiddlewareAsync(
            scope.ServiceProvider,
            request =>
            {
                request.Headers["X-Tenant-Id"] = tenant.Id.ToString();
            },
            user: null);

        Assert.True(nextCalled);
        Assert.Equal(200, statusCode);
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        Assert.True(currentTenant.IsResolved);
        Assert.Equal(tenant.Id, currentTenant.Id);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
    }

    [Fact]
    public async Task Anonymous_TrustedHeader_ValidActiveTenant_CannotObtainAccessPermission_ViaRealAccessServices()
    {
        var tenant = await SeedTenantAsync("Acme", "acme", TenantStatus.Active);

        using var scope = _provider.CreateScope();
        var (context, nextCalled, _) = await InvokeRealMiddlewareAsync(
            scope.ServiceProvider,
            request => request.Headers["X-Tenant-Id"] = tenant.Id.ToString(),
            user: null);

        Assert.True(nextCalled);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));

        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;
        var scopeContext = scope.ServiceProvider.GetRequiredService<IAccessScopeContext>();
        Assert.IsType<ClaimsAccessScopeContext>(scopeContext);
        Assert.Null(scopeContext.Current);

        var checker = scope.ServiceProvider.GetRequiredService<IPermissionChecker>();
        Assert.False(await checker.HasPermissionAsync("sales.create"));
    }

    [Fact]
    public async Task Authenticated_CurrentMember_PublishesAccessScope_AndCanObtainGrantedPermission()
    {
        var tenant = await SeedTenantAsync("Acme", "acme", TenantStatus.Active);
        var userId = Guid.NewGuid();
        await SeedTenantMemberAsync(tenant.Id, userId, isOwner: false);
        await SeedAccessGrantAsync(tenant.Id, userId, "sales.create");

        using var scope = _provider.CreateScope();
        var principal = AuthenticatedPrincipal(userId);
        var (context, nextCalled, statusCode) = await InvokeRealMiddlewareAsync(
            scope.ServiceProvider,
            request => request.Headers["X-Tenant-Id"] = tenant.Id.ToString(),
            user: principal);

        Assert.True(nextCalled);
        Assert.Equal(200, statusCode);
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        Assert.Equal(tenant.Id, currentTenant.Id);
        Assert.True(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
        Assert.Equal(tenant.Id, context.Items[AccessScopeContextItemKeys.TenantId]);

        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;
        var accessCurrentUser = scope.ServiceProvider.GetRequiredService<IAccessCurrentUser>();
        Assert.True(accessCurrentUser.IsAuthenticated);
        Assert.Equal(userId, accessCurrentUser.UserId);

        var scopeContext = scope.ServiceProvider.GetRequiredService<IAccessScopeContext>();
        var resolvedScope = scopeContext.Current;
        Assert.NotNull(resolvedScope);
        Assert.Equal(AccessScope.TenantType, resolvedScope!.Value.Type);
        Assert.Equal(tenant.Id, resolvedScope.Value.Id);

        var checker = scope.ServiceProvider.GetRequiredService<IPermissionChecker>();
        Assert.True(await checker.HasPermissionAsync("sales.create"));
        Assert.False(await checker.HasPermissionAsync("sales.refund"));
    }

    [Fact]
    public async Task Authenticated_NonMember_IsRefused_AndAccessScopeNotPublished()
    {
        var tenant = await SeedTenantAsync("Acme", "acme", TenantStatus.Active);
        var userId = Guid.NewGuid();

        using var scope = _provider.CreateScope();
        var principal = AuthenticatedPrincipal(userId);
        var (context, nextCalled, statusCode) = await InvokeRealMiddlewareAsync(
            scope.ServiceProvider,
            request => request.Headers["X-Tenant-Id"] = tenant.Id.ToString(),
            user: principal);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        Assert.False(currentTenant.IsResolved);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
    }

    [Fact]
    public async Task PreExistingScopeItem_IsCleared_WhenTenantIsInactive()
    {
        var tenant = await SeedTenantAsync("Acme", "acme", TenantStatus.Suspended);

        using var scope = _provider.CreateScope();
        var (context, nextCalled, statusCode) = await InvokeRealMiddlewareAsync(
            scope.ServiceProvider,
            request => request.Headers["X-Tenant-Id"] = tenant.Id.ToString(),
            user: null,
            preExistingScopeItem: Guid.NewGuid());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
    }

    [Fact]
    public async Task PreExistingScopeItem_IsCleared_WhenTenantIdentifiersMismatch()
    {
        var own = await SeedTenantAsync("Own", "own", TenantStatus.Active);
        var other = await SeedTenantAsync("Other", "other", TenantStatus.Active);
        var userId = Guid.NewGuid();
        await SeedTenantMemberAsync(own.Id, userId, isOwner: false);

        using var scope = _provider.CreateScope();
        var principal = AuthenticatedPrincipal(userId, tenantClaim: own.Id);
        var (context, nextCalled, statusCode) = await InvokeRealMiddlewareAsync(
            scope.ServiceProvider,
            request => request.Headers["X-Tenant-Id"] = other.Id.ToString(),
            user: principal,
            preExistingScopeItem: Guid.NewGuid());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
    }

    [Fact]
    public async Task PreExistingScopeItem_IsCleared_WhenAuthenticatedUserIdentityIsInvalid()
    {
        var tenant = await SeedTenantAsync("Acme", "acme", TenantStatus.Active);

        using var scope = _provider.CreateScope();
        var invalidPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "not-a-guid") }, "test"));
        var (context, nextCalled, statusCode) = await InvokeRealMiddlewareAsync(
            scope.ServiceProvider,
            request => request.Headers["X-Tenant-Id"] = tenant.Id.ToString(),
            user: invalidPrincipal,
            preExistingScopeItem: Guid.NewGuid());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
    }

    [Fact]
    public async Task PreExistingScopeItem_IsCleared_WhenAuthenticatedMembershipValidationFails()
    {
        var tenant = await SeedTenantAsync("Acme", "acme", TenantStatus.Active);
        var userId = Guid.NewGuid();

        using var scope = _provider.CreateScope();
        var principal = AuthenticatedPrincipal(userId);
        var (context, nextCalled, statusCode) = await InvokeRealMiddlewareAsync(
            scope.ServiceProvider,
            request => request.Headers["X-Tenant-Id"] = tenant.Id.ToString(),
            user: principal,
            preExistingScopeItem: Guid.NewGuid());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.False(context.Items.ContainsKey(AccessScopeContextItemKeys.TenantId));
    }

    [Fact]
    public async Task TenantOwner_DoesNotImplyAccessOwnerOrElevatedPermission()
    {
        var tenant = await SeedTenantAsync("Acme", "acme", TenantStatus.Active);
        var userId = Guid.NewGuid();
        await SeedTenantMemberAsync(tenant.Id, userId, isOwner: true);

        using var scope = _provider.CreateScope();
        var principal = AuthenticatedPrincipal(userId);
        var (context, nextCalled, statusCode) = await InvokeRealMiddlewareAsync(
            scope.ServiceProvider,
            request => request.Headers["X-Tenant-Id"] = tenant.Id.ToString(),
            user: principal);

        Assert.True(nextCalled);
        Assert.Equal(200, statusCode);
        Assert.Equal(tenant.Id, context.Items[AccessScopeContextItemKeys.TenantId]);

        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;

        var checker = scope.ServiceProvider.GetRequiredService<IPermissionChecker>();
        Assert.False(await checker.HasPermissionAsync("sales.create"));
        Assert.False(await checker.HasPermissionAsync(AccessPermissionNames.RolesManage));

        var access = scope.ServiceProvider.GetRequiredService<IAccessService>();
        var effective = await access.GetAccessAsync(userId, AccessScope.ForTenant(tenant.Id));
        Assert.True(effective.IsMember);
        Assert.False(effective.IsOwner);
        Assert.DoesNotContain("sales.create", effective.Permissions);
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(Guid userId, Guid? tenantClaim = null)
    {
        var claims = new List<Claim> { new("sub", userId.ToString()) };
        if (tenantClaim.HasValue)
            claims.Add(new Claim(TenantClaimTypes.TenantId, tenantClaim.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private async Task<(DefaultHttpContext Context, bool NextCalled, int StatusCode)> InvokeRealMiddlewareAsync(
        IServiceProvider scopedProvider,
        Action<HttpRequest> configureRequest,
        ClaimsPrincipal? user,
        Guid? preExistingScopeItem = null)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = scopedProvider;
        configureRequest(context.Request);
        context.User = user ?? new ClaimsPrincipal(new ClaimsIdentity());
        if (preExistingScopeItem.HasValue)
            context.Items[AccessScopeContextItemKeys.TenantId] = preExistingScopeItem.Value;

        var accessor = scopedProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;

        var membershipReader = scopedProvider.GetRequiredService<ITenantMembershipReader>();
        var strategies = scopedProvider.GetRequiredService<IEnumerable<ITenantResolutionStrategy>>();
        var provider = scopedProvider.GetRequiredService<ITenantInfoProvider>();
        var setter = scopedProvider.GetRequiredService<ITenantContextSetter>();

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Options.Create(new TenantOptions()),
            NullLogger<TenantResolutionMiddleware>.Instance);

        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context, membershipReader, strategies, provider, setter);
        return (context, nextCalled, context.Response.StatusCode == 0 ? 200 : context.Response.StatusCode);
    }

    private async Task<TenantEntity> SeedTenantAsync(string name, string slug, TenantStatus status)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            StatusChangedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private async Task SeedTenantMemberAsync(Guid tenantId, Guid userId, bool isOwner)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        db.Members.Add(new TenantMember
        {
            TenantId = tenantId,
            UserId = userId,
            IsOwner = isOwner,
            IsDefault = true,
            JoinedAt = DateTime.UtcNow,
            MembershipGeneration = 1
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedAccessGrantAsync(Guid tenantId, Guid userId, string permission)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var roleId = Guid.NewGuid();
        db.Roles.Add(new AccessRole
        {
            Id = roleId,
            ScopeType = AccessScope.TenantType,
            ScopeId = tenantId,
            Name = "MemberRole",
            NormalizedName = "MEMBERROLE",
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            Permissions = { new AccessRolePermission { RoleId = roleId, Permission = permission } }
        });
        db.Assignments.Add(new AccessAssignment
        {
            RoleId = roleId,
            UserId = userId,
            ScopeType = AccessScope.TenantType,
            ScopeId = tenantId,
            MembershipGeneration = 1,
            AssignedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
