namespace CoreKit.Tenant.Tests;

public sealed class TenantSlugTests
{
    [Theory]
    [InlineData("  Acme-Foods ", "acme-foods")]
    [InlineData("ACME", "acme")]
    public void Normalize_TrimsAndLowercases(string input, string expected)
        => Assert.Equal(expected, TenantSlug.Normalize(input));

    [Theory]
    [InlineData("acme")]
    [InlineData("acme-foods")]
    [InlineData("a1b")]
    [InlineData("abc-123")]
    [InlineData("a-b-c")]
    public void Validate_AcceptsGoodSlugs(string slug)
        => Assert.Null(TenantSlug.Validate(slug, TenantOptions.DefaultReservedSlugs));

    [Theory]
    [InlineData("ab")]
    [InlineData("-abc")]
    [InlineData("abc-")]
    [InlineData("ab--cd")]
    [InlineData("ab_cd")]
    [InlineData("ab cd")]
    [InlineData("ABC")]
    [InlineData("caf\u00e9s")]
    public void Validate_RejectsBadSlugs(string slug)
        => Assert.NotNull(TenantSlug.Validate(slug, TenantOptions.DefaultReservedSlugs));

    [Fact]
    public void Validate_RejectsTooLong()
        => Assert.NotNull(TenantSlug.Validate(new string('a', 64), TenantOptions.DefaultReservedSlugs));

    [Fact]
    public void Validate_AcceptsMaxLength()
        => Assert.Null(TenantSlug.Validate(new string('a', 63), TenantOptions.DefaultReservedSlugs));

    [Theory]
    [InlineData("admin")]
    [InlineData("api")]
    [InlineData("www")]
    public void Validate_RejectsReserved(string slug)
        => Assert.Contains("reserved", TenantSlug.Validate(slug, TenantOptions.DefaultReservedSlugs));

    [Theory]
    [InlineData("Acme Foods", "acme-foods")]
    [InlineData("  Caf\u00e9  D\u00e9lice!! ", "cafe-delice")]
    [InlineData("B\u00e4ckerei 24/7", "backerei-24-7")]
    [InlineData("A&B Traders", "a-b-traders")]
    [InlineData("!!!", "")]
    [InlineData("\u0627\u0631\u062f\u0648", "")]
    public void FromName_BuildsASlug(string name, string expected)
        => Assert.Equal(expected, TenantSlug.FromName(name));

    [Fact]
    public void FromName_CutsLongNamesWithoutATrailingHyphen()
    {
        var slug = TenantSlug.FromName(new string('a', 62) + " bbbb");

        Assert.True(slug.Length <= TenantSlug.MaxLength);
        Assert.False(slug.EndsWith('-'));
    }
}

public sealed class TenantLifecycleTests
{
    private static readonly HashSet<(TenantStatus, TenantStatus)> Allowed = new()
    {
        (TenantStatus.Pending, TenantStatus.Active),
        (TenantStatus.Pending, TenantStatus.Archived),
        (TenantStatus.Active, TenantStatus.Suspended),
        (TenantStatus.Active, TenantStatus.Archived),
        (TenantStatus.Suspended, TenantStatus.Active),
        (TenantStatus.Suspended, TenantStatus.Archived),
        (TenantStatus.Archived, TenantStatus.Suspended)
    };

    public static IEnumerable<object[]> AllPairs()
    {
        foreach (var from in Enum.GetValues<TenantStatus>())
            foreach (var to in Enum.GetValues<TenantStatus>())
                yield return new object[] { from, to };
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void CanTransition_MatchesTheLifecycleTable(TenantStatus from, TenantStatus to)
        => Assert.Equal(Allowed.Contains((from, to)), TenantLifecycle.CanTransition(from, to));
}

public sealed class TenantValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateName_RequiresAName(string? name)
        => Assert.NotNull(TenantValidation.ValidateName(name, out _));

    [Fact]
    public void ValidateName_TrimsAndAccepts()
    {
        Assert.Null(TenantValidation.ValidateName("  Acme  ", out var cleaned));
        Assert.Equal("Acme", cleaned);
    }

    [Fact]
    public void ValidateName_EnforcesTheLimit()
    {
        Assert.Null(TenantValidation.ValidateName(new string('a', 200), out _));
        Assert.NotNull(TenantValidation.ValidateName(new string('a', 201), out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ValidateReason_TreatsBlankAsNone(string? reason)
    {
        Assert.Null(TenantValidation.ValidateReason(reason, out var cleaned));
        Assert.Null(cleaned);
    }

    [Fact]
    public void ValidateReason_EnforcesTheLimit()
    {
        Assert.Null(TenantValidation.ValidateReason(new string('a', 500), out _));
        Assert.NotNull(TenantValidation.ValidateReason(new string('a', 501), out _));
    }
}

public sealed class TenantResultTests
{
    [Fact]
    public void Success_CarriesTheValue()
    {
        var result = TenantResult.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_HidesTheValue()
    {
        TenantResult<int> result = TenantErrors.NotFound("Tenant");

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ErrorConvertsToAnyResultType()
    {
        TenantResult plain = TenantErrors.Conflict("x");
        TenantResult<string> typed = TenantErrors.Conflict("x");

        Assert.Equal(TenantErrorKind.Conflict, plain.Error!.Kind);
        Assert.Equal(TenantErrorKind.Conflict, typed.Error!.Kind);
    }

    [Theory]
    [InlineData(TenantStatus.Pending, "tenant.pending")]
    [InlineData(TenantStatus.Suspended, "tenant.suspended")]
    [InlineData(TenantStatus.Archived, "tenant.archived")]
    public void ForStatus_NamesTheProblem(TenantStatus status, string code)
    {
        var error = TenantErrors.ForStatus(status);

        Assert.Equal(code, error.Code);
        Assert.Equal(TenantErrorKind.Forbidden, error.Kind);
    }

    [Fact]
    public void ForStatus_HasNoErrorForActive()
        => Assert.Throws<ArgumentOutOfRangeException>(() => TenantErrors.ForStatus(TenantStatus.Active));

    [Theory]
    [InlineData(TenantErrorKind.Validation, 400)]
    [InlineData(TenantErrorKind.Unauthorized, 401)]
    [InlineData(TenantErrorKind.Forbidden, 403)]
    [InlineData(TenantErrorKind.NotFound, 404)]
    [InlineData(TenantErrorKind.Conflict, 409)]
    public void StatusCodeFor_MapsEveryKind(TenantErrorKind kind, int expected)
        => Assert.Equal(expected, CoreKit.Tenant.Endpoints.TenantResultExtensions.StatusCodeFor(kind));
}

public sealed class TenantOptionsTests
{
    [Fact]
    public void Defaults_AreSafe()
    {
        var options = new TenantOptions();

        Assert.False(options.Resolution.TrustHeader);
        Assert.Equal("X-Tenant-Id", options.Resolution.HeaderName);
        Assert.Equal("tenant_id", options.Resolution.ClaimType);
        Assert.Equal(30, options.InfoCacheSeconds);
    }

    [Fact]
    public void ReservedSlugs_CombineDefaultsWithConfiguredOnes()
    {
        var options = new TenantOptions { AdditionalReservedSlugs = { " Shop ", "", "  " } };

        var reserved = options.GetReservedSlugs();

        Assert.Contains("admin", reserved);
        Assert.Contains("shop", reserved);
        Assert.DoesNotContain("", reserved);
    }
}

public sealed class TenantPermissionTests
{
    [Fact]
    public void Names_AreUniqueLowercaseAndPrefixed()
    {
        var names = TenantPermissionNames.All.Select(p => p.Name).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, n => Assert.StartsWith("tenants.", n));
        Assert.All(names, n => Assert.Equal(n.ToLowerInvariant(), n));
        Assert.Contains(TenantPermissionNames.Platform, names);
    }

    [Fact]
    public void IamSeeds_MirrorTheDefinitions()
    {
        var seeds = CoreKit.Tenant.Integration.TenantPermissionSeeds.All;

        Assert.Equal(TenantPermissionNames.All.Count, seeds.Count);
        Assert.Equal(
            TenantPermissionNames.All.Select(p => p.Name).Order(),
            seeds.Select(s => s.Name).Order());
    }
}