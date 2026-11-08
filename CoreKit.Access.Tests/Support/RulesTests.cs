using Microsoft.Extensions.Caching.Memory;

namespace CoreKit.Access.Tests;

public sealed class AccessScopeTests
{
    [Fact]
    public void ForTenant_UsesTheTenantType()
    {
        var id = Guid.NewGuid();

        var scope = AccessScope.ForTenant(id);

        Assert.Equal("tenant", scope.Type);
        Assert.Equal(id, scope.Id);
        Assert.True(scope.IsValid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("store", true)]
    public void IsValid_NeedsATypeAndAnId(string type, bool expected)
        => Assert.Equal(expected, new AccessScope(type, Guid.NewGuid()).IsValid);

    [Fact]
    public void IsValid_RejectsAnEmptyIdAndATooLongType()
    {
        Assert.False(new AccessScope("tenant", Guid.Empty).IsValid);
        Assert.False(new AccessScope(new string('a', 31), Guid.NewGuid()).IsValid);
        Assert.True(new AccessScope(new string('a', 30), Guid.NewGuid()).IsValid);
    }

    [Fact]
    public void Scopes_AreComparedByValue_AndRenderWithoutDashes()
    {
        var id = Guid.NewGuid();

        Assert.Equal(AccessScope.ForTenant(id), AccessScope.ForTenant(id));
        Assert.NotEqual(AccessScope.ForTenant(id), new AccessScope("store", id));
        Assert.Equal($"tenant:{id:N}", AccessScope.ForTenant(id).ToString());
    }
}

public sealed class AccessCatalogTests
{
    private static AccessCatalog Build(params Action<AccessCatalogBuilder>[] configures)
        => AccessCatalog.Build(configures.Select(c => new AccessCatalogContribution(c)));

    [Fact]
    public void EveryCatalogContainsAccessOwnPermissions()
    {
        var catalog = Build();

        foreach (var permission in AccessPermissionNames.All)
            Assert.True(catalog.Contains(permission.Name));
    }

    [Fact]
    public void Add_NormalizesNames_AndKeepsTheFirstDescription()
    {
        var catalog = Build(b => b.Add("  Sales.Create ", "first").Add("sales.create", "second"));

        var permission = Assert.Single(catalog.Permissions, p => p.Name == "sales.create");
        Assert.Equal("first", permission.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData(".leading")]
    [InlineData("trailing.")]
    [InlineData("a..b")]
    [InlineData("under$core")]
    public void Add_RejectsInvalidNames(string name)
        => Assert.Throws<ArgumentException>(() => new AccessCatalogBuilder().Add(name, "x"));

    [Fact]
    public void Add_RejectsTooLongNames()
        => Assert.Throws<ArgumentException>(() => new AccessCatalogBuilder().Add(new string('a', 101), "x"));

    [Fact]
    public void Contributions_AreCombined_AndPermissionsAreSortedByName()
    {
        var catalog = Build(b => b.Add("zeta.use", "z"), b => b.Add("alpha.use", "a"));

        var names = catalog.Permissions.Select(p => p.Name).ToList();

        Assert.Contains("zeta.use", names);
        Assert.Contains("alpha.use", names);
        Assert.Equal(names.Order(StringComparer.Ordinal).ToList(), names);
        Assert.Contains("alpha.use", catalog.Names);
    }

    [Fact]
    public void RoleTemplates_AreKept_WithNormalizedDistinctPermissions()
    {
        var catalog = Build(b => b.Add("sales.create", "x").AddRoleTemplate("Cashier", "Sells", "Sales.Create", "sales.create"));

        var template = Assert.Single(catalog.RoleTemplates);
        Assert.Equal("Cashier", template.Name);
        Assert.Equal(new[] { "sales.create" }, template.Permissions);
    }

    [Fact]
    public void RoleTemplate_WithAnUndeclaredPermission_FailsWhenTheCatalogIsBuilt()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Build(b => b.AddRoleTemplate("Cashier", null, "never.declared")));

        Assert.Contains("never.declared", error.Message);
    }

    [Fact]
    public void RoleTemplate_NeedsAName()
        => Assert.Throws<ArgumentException>(() => new AccessCatalogBuilder().AddRoleTemplate("  ", null));
}

public sealed class AccessValidationTests
{
    private static readonly AccessCatalog Catalog = AccessCatalog.Build(new[]
    {
        new AccessCatalogContribution(b => b.Add("sales.create", "x").Add("sales.refund", "y"))
    });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateName_RequiresAName(string? name)
        => Assert.NotNull(AccessValidation.ValidateName(name, out _));

    [Fact]
    public void ValidateName_TrimsAndEnforcesTheLimit()
    {
        Assert.Null(AccessValidation.ValidateName("  Cashier ", out var cleaned));
        Assert.Equal("Cashier", cleaned);
        Assert.Null(AccessValidation.ValidateName(new string('a', 100), out _));
        Assert.NotNull(AccessValidation.ValidateName(new string('a', 101), out _));
    }

    [Fact]
    public void ValidateDescription_TreatsBlankAsNone_AndEnforcesTheLimit()
    {
        Assert.Null(AccessValidation.ValidateDescription("   ", out var cleaned));
        Assert.Null(cleaned);
        Assert.Null(AccessValidation.ValidateDescription(new string('a', 500), out _));
        Assert.NotNull(AccessValidation.ValidateDescription(new string('a', 501), out _));
    }

    [Fact]
    public void ValidatePermissions_NormalizesAndRemovesDuplicates()
    {
        Assert.Null(AccessValidation.ValidatePermissions(new[] { " Sales.Create ", "sales.create", "sales.refund" }, Catalog, out var cleaned));
        Assert.Equal(new[] { "sales.create", "sales.refund" }, cleaned);
    }

    [Fact]
    public void ValidatePermissions_AcceptsNoneAtAll()
    {
        Assert.Null(AccessValidation.ValidatePermissions(null, Catalog, out var cleaned));
        Assert.Empty(cleaned);
    }

    [Fact]
    public void ValidatePermissions_RejectsUnknownAndEmptyNames()
    {
        Assert.NotNull(AccessValidation.ValidatePermissions(new[] { "nope.nothing" }, Catalog, out _));
        Assert.NotNull(AccessValidation.ValidatePermissions(new[] { "  " }, Catalog, out _));
    }

    [Fact]
    public void NormalizeName_IsCaseInsensitive()
        => Assert.Equal(AccessValidation.NormalizeName("Cashier"), AccessValidation.NormalizeName("  CASHIER "));
}

public sealed class EffectiveAccessTests
{
    [Fact]
    public void None_GrantsNothing()
    {
        Assert.False(EffectiveAccess.None.IsMember);
        Assert.False(EffectiveAccess.None.Has("anything"));
        Assert.Empty(EffectiveAccess.None.Permissions);
        Assert.Empty(EffectiveAccess.None.Roles);
    }

    [Fact]
    public void AMember_HasOnlyTheirPermissions()
    {
        var access = new EffectiveAccess(true, false, new HashSet<string> { "sales.create" }, new[] { "Cashier" });

        Assert.True(access.Has("sales.create"));
        Assert.False(access.Has("sales.refund"));
    }

    [Fact]
    public void AnOwner_HasEveryPermission_EvenOnesNobodyDeclared()
    {
        var access = new EffectiveAccess(true, true, new HashSet<string>(), Array.Empty<string>());

        Assert.True(access.Has("anything.at.all"));
    }

    [Fact]
    public void ANonMember_HasNothing_EvenWithPermissionsOrOwnerFlag()
    {
        var access = new EffectiveAccess(false, true, new HashSet<string> { "sales.create" }, Array.Empty<string>());

        Assert.False(access.Has("sales.create"));
    }
}

public sealed class AccessCacheTests
{
    private readonly MemoryCache _memory = new(new MemoryCacheOptions());

    private AccessCache Cache(int seconds = 30)
        => new(_memory, Options.Create(new AccessOptions { CacheSeconds = seconds }));

    private static readonly AccessScope ScopeA = AccessScope.ForTenant(Guid.NewGuid());
    private static readonly AccessScope ScopeB = AccessScope.ForTenant(Guid.NewGuid());

    private static EffectiveAccess Access(params string[] permissions)
        => new(true, false, permissions.ToHashSet(), Array.Empty<string>());

    [Fact]
    public void Set_ThenTryGet_ReturnsTheSameAccess()
    {
        var cache = Cache();
        var user = Guid.NewGuid();
        var access = Access("a.b");

        cache.Set(ScopeA, user, access);

        Assert.True(cache.TryGet(ScopeA, user, out var found));
        Assert.Same(access, found);
    }

    [Fact]
    public void TryGet_MissesForOtherUsersAndOtherScopes()
    {
        var cache = Cache();
        var user = Guid.NewGuid();
        cache.Set(ScopeA, user, Access("a.b"));

        Assert.False(cache.TryGet(ScopeA, Guid.NewGuid(), out _));
        Assert.False(cache.TryGet(ScopeB, user, out _));
    }

    [Fact]
    public void ZeroSecondsTurnsTheCacheOff()
    {
        var cache = Cache(0);
        var user = Guid.NewGuid();

        cache.Set(ScopeA, user, Access("a.b"));

        Assert.False(cache.TryGet(ScopeA, user, out _));
    }

    [Fact]
    public void InvalidateUser_ForgetsOnlyThatUser()
    {
        var cache = Cache();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        cache.Set(ScopeA, first, Access("a.b"));
        cache.Set(ScopeA, second, Access("a.b"));

        cache.InvalidateUser(ScopeA, first);

        Assert.False(cache.TryGet(ScopeA, first, out _));
        Assert.True(cache.TryGet(ScopeA, second, out _));
    }

    [Fact]
    public void InvalidateScope_ForgetsEveryoneInThatScopeOnly()
    {
        var cache = Cache();
        var user = Guid.NewGuid();
        cache.Set(ScopeA, user, Access("a.b"));
        cache.Set(ScopeB, user, Access("a.b"));

        cache.InvalidateScope(ScopeA);

        Assert.False(cache.TryGet(ScopeA, user, out _));
        Assert.True(cache.TryGet(ScopeB, user, out _));
    }

    [Fact]
    public void AfterInvalidateScope_NewEntriesAreRememberedAgain()
    {
        var cache = Cache();
        var user = Guid.NewGuid();
        cache.Set(ScopeA, user, Access("a.b"));
        cache.InvalidateScope(ScopeA);

        cache.Set(ScopeA, user, Access("c.d"));

        Assert.True(cache.TryGet(ScopeA, user, out var found));
        Assert.True(found.Has("c.d"));
    }
}

public sealed class AccessResultTests
{
    [Fact]
    public void Success_CarriesTheValue()
    {
        var result = AccessResult.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_HidesTheValue()
    {
        AccessResult<int> result = AccessErrors.NotFound("Role");

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Theory]
    [InlineData(AccessErrorKind.Validation, 400)]
    [InlineData(AccessErrorKind.Unauthorized, 401)]
    [InlineData(AccessErrorKind.Forbidden, 403)]
    [InlineData(AccessErrorKind.NotFound, 404)]
    [InlineData(AccessErrorKind.Conflict, 409)]
    public void StatusCodeFor_MapsEveryKind(AccessErrorKind kind, int expected)
        => Assert.Equal(expected, CoreKit.Access.Endpoints.AccessResultExtensions.StatusCodeFor(kind));

    [Fact]
    public void TheNamedErrorsHaveStableCodesAndKinds()
    {
        Assert.Equal(("access.scope_required", AccessErrorKind.Validation), (AccessErrors.ScopeRequired.Code, AccessErrors.ScopeRequired.Kind));
        Assert.Equal(("access.not_authenticated", AccessErrorKind.Unauthorized), (AccessErrors.NotAuthenticated.Code, AccessErrors.NotAuthenticated.Kind));
        Assert.Equal(("access.denied", AccessErrorKind.Forbidden), (AccessErrors.Denied.Code, AccessErrors.Denied.Kind));
        Assert.Equal(("access.escalation", AccessErrorKind.Forbidden), (AccessErrors.Escalation.Code, AccessErrors.Escalation.Kind));
        Assert.Equal(("access.save_conflict", AccessErrorKind.Conflict), (AccessErrors.SaveConflict.Code, AccessErrors.SaveConflict.Kind));
    }
}

public sealed class AccessOptionsTests
{
    [Fact]
    public void Defaults_AreSensible()
    {
        var options = new AccessOptions();

        Assert.Equal(50, options.MaxRolesPerScope);
        Assert.Equal(20, options.MaxRolesPerUser);
        Assert.Equal(30, options.CacheSeconds);
        Assert.Equal("sub", options.UserIdClaimType);
    }
}