using System.Collections.Concurrent;
using System.Security.Claims;
using CoreKit.Access.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreKit.Access.Tests;

/// <summary>
/// Proves the background execution context: a grant is issued only from an authorized request, a job runs with
/// that scope and actor, the permission is re-checked on fresh data, and nothing leaks between jobs.
/// </summary>
public sealed class AccessBackgroundContextTests : IDisposable
{
    private const string Permission = "test.do";
    private const string OtherPermission = "test.other";
    private const string Job = "test.job";

    private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=True");
    private readonly ManualTime _time = new() { Now = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero) };
    private readonly StubMembership _members = new();
    private readonly ServiceProvider _provider;

    public AccessBackgroundContextTests()
    {
        _connection.Open();
        _provider = BuildServices().BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AccessDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_time);
        services.AddAccess(new ConfigurationBuilder().Build(), o => o.UseSqlite(_connection));
        services.AddAccessPermissions(p => p.Add(Permission, "Test permission").Add(OtherPermission, "Other permission"));
        services.AddSingleton<IAccessMembershipAdapter>(_members);
        services.AddAccessBackgroundContext();
        // A deterministic key ring for tests: each provider gets its own, so a token never crosses providers.
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        return services;
    }

    private IAccessBackgroundExecutor Executor => _provider.GetRequiredService<IAccessBackgroundExecutor>();

    /// <summary>Issues a grant the way a workflow does: inside an authenticated, tenant-validated request.</summary>
    private async Task<AccessResult<AccessBackgroundGrantToken>> IssueAsHttp(
        Guid tenant, Guid user, string permission = Permission, TimeSpan? lifetime = null, bool authenticated = true)
    {
        var accessor = _provider.GetRequiredService<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Items[AccessScopeContextItemKeys.TenantId] = tenant;
        context.User = authenticated
            ? new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", user.ToString()) }, "test"))
            : new ClaimsPrincipal(new ClaimsIdentity());
        accessor.HttpContext = context;

        try
        {
            using var scope = _provider.CreateScope();
            return await scope.ServiceProvider
                .GetRequiredService<IAccessBackgroundGrantIssuer>()
                .IssueAsync(Job, permission, lifetime);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    /// <summary>Issues a platform-level grant: an authenticated request with no tenant scope item.</summary>
    private async Task<AccessResult<AccessBackgroundGrantToken>> IssuePlatformAsHttp(Guid user)
    {
        var accessor = _provider.GetRequiredService<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", user.ToString()) }, "test"));
        accessor.HttpContext = context;

        try
        {
            using var scope = _provider.CreateScope();
            return await scope.ServiceProvider
                .GetRequiredService<IAccessBackgroundGrantIssuer>()
                .IssueAsync(Job, Permission);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    [Fact]
    public async Task AGrantFromAnAuthorizedRequest_RunsWithThatScopeAndActor_AndAccessAuthorizesIt()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        _members.Allow(tenant, user);
        var grant = (await IssueAsHttp(tenant, user)).Value;

        AccessScope? scopeSeen = null;
        Guid? userSeen = null;
        bool? authenticated = null;
        bool? allowed = null;
        var active = false;

        var status = await Executor.RunAsync(grant, Job, Permission, async (sp, ct) =>
        {
            scopeSeen = sp.GetRequiredService<IAccessScopeContext>().Current;
            userSeen = sp.GetRequiredService<IAccessCurrentUser>().UserId;
            authenticated = sp.GetRequiredService<IAccessCurrentUser>().IsAuthenticated;
            active = sp.GetRequiredService<IAccessBackgroundContext>().IsActive;
            allowed = await sp.GetRequiredService<IPermissionChecker>().HasPermissionAsync(Permission, ct);
        });

        Assert.Equal(AccessBackgroundRunStatus.Completed, status);
        Assert.Equal(AccessScope.ForTenant(tenant), scopeSeen);
        Assert.Equal(user, userSeen);
        Assert.True(authenticated);
        Assert.True(active);
        Assert.True(allowed);
    }

    [Fact]
    public async Task AGrantIssuedWithoutATenantScope_RunsAsAPlatformJob()
    {
        var user = Guid.NewGuid();
        _members.AllowPlatform(AccessScope.PlatformId, user);
        var grant = (await IssuePlatformAsHttp(user)).Value;

        AccessScope? scopeSeen = null;
        Guid? userSeen = null;
        bool? platformAllowed = null;
        bool? tenantAllowed = null;

        var status = await Executor.RunAsync(grant, Job, Permission, async (sp, ct) =>
        {
            scopeSeen = sp.GetRequiredService<IAccessScopeContext>().Current;
            userSeen = sp.GetRequiredService<IAccessCurrentUser>().UserId;
            var checker = sp.GetRequiredService<IPermissionChecker>();
            platformAllowed = await checker.HasPlatformPermissionAsync(Permission, ct);
            tenantAllowed = await checker.HasPermissionAsync(Permission, ct);
        });

        Assert.Equal(AccessBackgroundRunStatus.Completed, status);
        Assert.Null(scopeSeen);
        Assert.Equal(user, userSeen);
        Assert.True(platformAllowed);
        // A platform job never falls back to a tenant check, and a tenant check never falls back to platform.
        Assert.False(tenantAllowed);
    }

    [Fact]
    public async Task AfterTheJob_NothingRemains_InThatScopeOrInAnyNewScope()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        _members.Allow(tenant, user);
        var grant = (await IssueAsHttp(tenant, user)).Value;
        IAccessBackgroundContext? jobContext = null;

        await Executor.RunAsync(grant, Job, Permission, (sp, ct) =>
        {
            jobContext = sp.GetRequiredService<IAccessBackgroundContext>();
            return Task.CompletedTask;
        });

        // The job's own holder is deactivated before its scope is disposed.
        Assert.False(jobContext!.IsActive);

        using var fresh = _provider.CreateScope();
        Assert.False(fresh.ServiceProvider.GetRequiredService<IAccessBackgroundContext>().IsActive);
        Assert.Null(fresh.ServiceProvider.GetRequiredService<IAccessScopeContext>().Current);
        Assert.Null(fresh.ServiceProvider.GetRequiredService<IAccessCurrentUser>().UserId);
    }

    [Fact]
    public async Task TheJob_NeverReadsTheHttpContextItIsStartedInside()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        _members.Allow(tenant, user);
        var grant = (await IssueAsHttp(tenant, user)).Value;

        var accessor = _provider.GetRequiredService<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Items[AccessScopeContextItemKeys.TenantId] = Guid.NewGuid();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", Guid.NewGuid().ToString()) }, "test"));
        accessor.HttpContext = context;

        AccessScope? scopeSeen = null;
        Guid? userSeen = null;
        try
        {
            await Executor.RunAsync(grant, Job, Permission, (sp, ct) =>
            {
                scopeSeen = sp.GetRequiredService<IAccessScopeContext>().Current;
                userSeen = sp.GetRequiredService<IAccessCurrentUser>().UserId;
                return Task.CompletedTask;
            });
        }
        finally
        {
            accessor.HttpContext = null;
        }

        Assert.Equal(AccessScope.ForTenant(tenant), scopeSeen);
        Assert.Equal(user, userSeen);
    }

    [Fact]
    public async Task AGrantForAnotherJobKind_IsRefused_AndTheWorkDoesNotRun()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        _members.Allow(tenant, user);
        var grant = (await IssueAsHttp(tenant, user)).Value;
        var ran = false;

        var status = await Executor.RunAsync(grant, "some.other.job", Permission, (sp, ct) =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.Equal(AccessBackgroundRunStatus.GrantMismatch, status);
        Assert.False(ran);
    }

    [Fact]
    public async Task AGrantIssuedForOnePermission_IsRefusedWhenRedeemedForAnother()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        _members.Allow(tenant, user);
        var grant = (await IssueAsHttp(tenant, user, Permission)).Value;
        var ran = false;

        var status = await Executor.RunAsync(grant, Job, OtherPermission, (sp, ct) =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.Equal(AccessBackgroundRunStatus.GrantMismatch, status);
        Assert.False(ran);
    }

    [Fact]
    public async Task AnExpiredGrant_IsRefused()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        _members.Allow(tenant, user);
        var grant = (await IssueAsHttp(tenant, user, lifetime: TimeSpan.FromMinutes(10))).Value;
        var ran = false;

        _time.Now = _time.Now.AddMinutes(10);
        var status = await Executor.RunAsync(grant, Job, Permission, (sp, ct) =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.Equal(AccessBackgroundRunStatus.GrantExpired, status);
        Assert.False(ran);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-grant")]
    [InlineData("CfDJ8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task ForgedOrGarbageGrants_AreRefused_AsInvalid(string token)
    {
        var ran = false;

        var status = await Executor.RunAsync(new AccessBackgroundGrantToken(token), Job, Permission, (sp, ct) =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.Equal(AccessBackgroundRunStatus.GrantInvalid, status);
        Assert.False(ran);
    }

    [Fact]
    public async Task ATamperedGrant_IsRefused()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        _members.Allow(tenant, user);
        var value = (await IssueAsHttp(tenant, user)).Value.Value;
        var tampered = value[..^4] + (value.EndsWith("AAAA") ? "BBBB" : "AAAA");

        var status = await Executor.RunAsync(
            new AccessBackgroundGrantToken(tampered), Job, Permission, (sp, ct) => Task.CompletedTask);

        Assert.Equal(AccessBackgroundRunStatus.GrantInvalid, status);
    }

    [Fact]
    public async Task APermissionRevokedBetweenIssueAndRun_StopsTheJob()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        _members.Allow(tenant, user);
        var grant = (await IssueAsHttp(tenant, user)).Value;
        var ran = false;

        _members.Revoke(tenant, user);
        var status = await Executor.RunAsync(grant, Job, Permission, (sp, ct) =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.Equal(AccessBackgroundRunStatus.NotAuthorized, status);
        Assert.False(ran);
    }

    [Fact]
    public async Task IssuingRequiresAnAuthenticatedActorWhoHoldsThePermission_AndAValidLifetime()
    {
        var tenant = Guid.NewGuid();
        var member = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        _members.Allow(tenant, member);

        Assert.True((await IssueAsHttp(tenant, member)).IsSuccess);
        Assert.Equal("access.denied", (await IssueAsHttp(tenant, outsider)).Error!.Code);
        Assert.Equal("access.not_authenticated", (await IssueAsHttp(tenant, member, authenticated: false)).Error!.Code);
        Assert.True((await IssueAsHttp(tenant, member, lifetime: TimeSpan.FromHours(2))).IsFailure);
        Assert.True((await IssueAsHttp(tenant, member, lifetime: TimeSpan.Zero)).IsFailure);
    }

    [Fact]
    public async Task AJobCannotIssueNewGrants()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        _members.Allow(tenant, user);
        var grant = (await IssueAsHttp(tenant, user)).Value;
        AccessResult<AccessBackgroundGrantToken>? nested = null;

        await Executor.RunAsync(grant, Job, Permission, async (sp, ct) =>
        {
            nested = await sp.GetRequiredService<IAccessBackgroundGrantIssuer>().IssueAsync(Job, Permission, null, ct);
        });

        Assert.True(nested!.IsFailure);
        Assert.Equal("access.denied", nested.Error!.Code);
    }

    [Fact]
    public async Task TheSystemActor_IsNotAUser_AndEveryUserPermissionCheckDeniesIt()
    {
        var tenant = Guid.NewGuid();
        _members.AllowEveryone(tenant);
        Guid? userSeen = Guid.NewGuid();
        bool? authenticated = null;
        bool? allowed = null;
        AccessScope? scopeSeen = null;

        var status = await Executor.RunAsSystemAsync("system.job", AccessScope.ForTenant(tenant), async (sp, ct) =>
        {
            userSeen = sp.GetRequiredService<IAccessCurrentUser>().UserId;
            authenticated = sp.GetRequiredService<IAccessCurrentUser>().IsAuthenticated;
            scopeSeen = sp.GetRequiredService<IAccessScopeContext>().Current;
            allowed = await sp.GetRequiredService<IPermissionChecker>().HasPermissionAsync(Permission, ct);
        });

        Assert.Equal(AccessBackgroundRunStatus.Completed, status);
        Assert.Null(userSeen);
        Assert.False(authenticated);
        Assert.Equal(AccessScope.ForTenant(tenant), scopeSeen);
        Assert.False(allowed);
    }

    [Fact]
    public async Task MalformedSystemContexts_AreRefused()
    {
        var ran = false;
        Task Work(IServiceProvider sp, CancellationToken ct)
        {
            ran = true;
            return Task.CompletedTask;
        }

        Assert.Equal(AccessBackgroundRunStatus.ContextInvalid,
            await Executor.RunAsSystemAsync("", null, Work));
        Assert.Equal(AccessBackgroundRunStatus.ContextInvalid,
            await Executor.RunAsSystemAsync("system.job", AccessScope.ForTenant(Guid.Empty), Work));
        Assert.Equal(AccessBackgroundRunStatus.ContextInvalid,
            await Executor.RunAsSystemAsync("system.job", AccessScope.ForPlatform(AccessScope.PlatformId), Work));
        Assert.False(ran);
    }

    [Fact]
    public async Task ConcurrentJobs_EachSeeOnlyTheirOwnScopeAndActor_AndOnlyTheirOwnAuthorization()
    {
        var jobs = Enumerable.Range(0, 20)
            .Select(_ => (Tenant: Guid.NewGuid(), User: Guid.NewGuid()))
            .ToList();

        foreach (var job in jobs)
            _members.Allow(job.Tenant, job.User);

        var grants = new List<AccessBackgroundGrantToken>();
        foreach (var job in jobs)
            grants.Add((await IssueAsHttp(job.Tenant, job.User)).Value);

        var observations = new ConcurrentBag<(bool ScopeOk, bool UserOk, bool OwnAllowed, bool OtherDenied)>();

        await Task.WhenAll(jobs.Select((job, i) => Executor.RunAsync(grants[i], Job, Permission, async (sp, ct) =>
        {
            await Task.Delay(Random.Shared.Next(1, 25), ct);

            var scope = sp.GetRequiredService<IAccessScopeContext>().Current;
            var user = sp.GetRequiredService<IAccessCurrentUser>().UserId;
            var checker = sp.GetRequiredService<IPermissionChecker>();
            var access = sp.GetRequiredService<IAccessService>();
            var other = jobs[(i + 1) % jobs.Count];

            observations.Add((
                scope == AccessScope.ForTenant(job.Tenant),
                user == job.User,
                await checker.HasPermissionAsync(Permission, ct),
                !(await access.GetAccessAsync(job.User, AccessScope.ForTenant(other.Tenant), ct)).Has(Permission)));
        })));

        Assert.Equal(jobs.Count, observations.Count);
        Assert.All(observations, o =>
        {
            Assert.True(o.ScopeOk);
            Assert.True(o.UserOk);
            Assert.True(o.OwnAllowed);
            Assert.True(o.OtherDenied);
        });
    }

    [Fact]
    public void RegisteringBeforeAccess_FailsFast()
        => Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAccessBackgroundContext());

    [Fact]
    public void RegisteringTwice_FailsFast()
    {
        var services = BuildServices();
        Assert.Throws<InvalidOperationException>(() => services.AddAccessBackgroundContext());
    }

    [Fact]
    public void TheWrapperAlsoWrapsAFactoryRegistration()
    {
        // Identity registers IAccessCurrentUser with a factory rather than a type, so the wrapper must handle
        // that shape too.
        var id = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_time);
        services.AddAccess(new ConfigurationBuilder().Build(), o => o.UseSqlite(_connection));
        services.AddScoped<IAccessCurrentUser>(sp => new StubCurrentUser(id));
        services.AddAccessBackgroundContext();

        using var provider = services.BuildServiceProvider();
        AccessBackgroundContextStartupCheck.Verify(provider);
        using var scope = provider.CreateScope();

        var wrapped = scope.ServiceProvider.GetRequiredService<IAccessCurrentUser>();
        Assert.IsType<BackgroundAwareCurrentUser>(wrapped);
        Assert.Equal(id, wrapped.UserId);
    }

    [Fact]
    public void TheStartupCheck_PassesWhenWrapped_AndFailsWhenALaterRegistrationReplacesTheWrapper()
    {
        AccessBackgroundContextStartupCheck.Verify(_provider);

        var services = BuildServices();
        services.AddScoped<IAccessScopeContext, ClaimsAccessScopeContext>();
        using var broken = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => AccessBackgroundContextStartupCheck.Verify(broken));
    }

    /// <summary>
    /// A membership stub the tests control. Tenant membership is decided by an explicit allow list, so a test
    /// can revoke a person between issuing a grant and running the job.
    /// </summary>
    private sealed class StubMembership : IAccessMembershipAdapter
    {
        private readonly ConcurrentDictionary<(Guid Tenant, Guid User), bool> _members = new();
        private readonly ConcurrentDictionary<(Guid Platform, Guid User), bool> _platformMembers = new();
        private readonly ConcurrentDictionary<Guid, bool> _everyone = new();

        public void Allow(Guid tenant, Guid user) => _members[(tenant, user)] = true;

        public void AllowPlatform(Guid platform, Guid user) => _platformMembers[(platform, user)] = true;

        public void Revoke(Guid tenant, Guid user) => _members.TryRemove((tenant, user), out _);

        public void AllowEveryone(Guid tenant) => _everyone[tenant] = true;

        public Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
        {
            if (scope.Type == AccessScope.PlatformType)
            {
                var platformMember = scope.Id == AccessScope.PlatformId && _platformMembers.ContainsKey((scope.Id, userId));
                return Task.FromResult<AccessMember?>(platformMember ? new AccessMember(true, 1) : null);
            }

            var allowed = scope.Type == AccessScope.TenantType
                          && (_members.ContainsKey((scope.Id, userId)) || _everyone.ContainsKey(scope.Id));
            return Task.FromResult<AccessMember?>(allowed ? new AccessMember(true, 1) : null);
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class StubCurrentUser : IAccessCurrentUser
    {
        private readonly Guid _id;

        public StubCurrentUser(Guid id) => _id = id;

        public Guid? UserId => _id;

        public bool IsAuthenticated => true;
    }
}