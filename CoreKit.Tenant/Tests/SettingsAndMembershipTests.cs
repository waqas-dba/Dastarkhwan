namespace CoreKit.Tenant.Tests;

public sealed class TenantSettingsServiceTests : IDisposable
{
    private readonly TestEnv _env = new();
    private readonly TenantEntity _tenant;

    public TenantSettingsServiceTests() => _tenant = _env.AddTenant("Acme", "acme");

    public void Dispose() => _env.Dispose();

    private TenantSettingsService Svc => _env.CreateSettingsService();

    private static SetTenantSettingRequest Value(string value) => new() { Value = value };

    [Fact]
    public async Task Set_CreatesASetting_AndNormalizesTheKey()
    {
        var dto = ResultAssert.Ok(await Svc.SetAsync(_tenant.Id, "  General.TimeZone ", Value("Asia/Karachi")));

        Assert.Equal("general.timezone", dto.Key);
        Assert.Equal("Asia/Karachi", dto.Value);
        Assert.Equal(_env.UtcNow, dto.UpdatedAt);
        Assert.Single(_env.Read(db => db.Settings.ToList()));
    }

    [Fact]
    public async Task Set_UpdatesAnExistingSetting()
    {
        _env.AddSetting(_tenant.Id, "general.timezone", "UTC");
        _env.Time.Advance(TimeSpan.FromMinutes(5));

        var dto = ResultAssert.Ok(await Svc.SetAsync(_tenant.Id, "general.timezone", Value("Asia/Karachi")));

        Assert.Equal("Asia/Karachi", dto.Value);
        var row = Assert.Single(_env.Read(db => db.Settings.AsNoTracking().ToList()));
        Assert.Equal("Asia/Karachi", row.Value);
        Assert.Equal(_env.UtcNow, row.UpdatedAt);
    }

    [Fact]
    public async Task Set_AllowsAnEmptyValue()
    {
        var dto = ResultAssert.Ok(await Svc.SetAsync(_tenant.Id, "note", Value(string.Empty)));

        Assert.Equal(string.Empty, dto.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("bad key!")]
    [InlineData(".leading")]
    [InlineData("trailing.")]
    [InlineData("a..b")]
    [InlineData("under$core")]
    public async Task Set_RejectsInvalidKeys(string key)
        => ResultAssert.Fails(await Svc.SetAsync(_tenant.Id, key, Value("x")), TenantErrorKind.Validation);

    [Fact]
    public async Task Set_EnforcesTheKeyAndValueLimits()
    {
        ResultAssert.Ok(await Svc.SetAsync(_tenant.Id, new string('a', 100), Value(new string('v', 4000))));

        ResultAssert.Fails(
            await Svc.SetAsync(_tenant.Id, new string('a', 101), Value("x")), TenantErrorKind.Validation);
        ResultAssert.Fails(
            await Svc.SetAsync(_tenant.Id, "key", Value(new string('v', 4001))), TenantErrorKind.Validation);
    }

    [Fact]
    public async Task SetMany_IsAllOrNothing()
    {
        var request = new SetTenantSettingsRequest
        {
            Values = new Dictionary<string, string> { ["good.key"] = "1", ["bad key"] = "2" }
        };

        ResultAssert.Fails(await Svc.SetManyAsync(_tenant.Id, request), TenantErrorKind.Validation);
        Assert.Empty(_env.Read(db => db.Settings.ToList()));
    }

    [Fact]
    public async Task SetMany_SavesEverything_InKeyOrder()
    {
        var request = new SetTenantSettingsRequest
        {
            Values = new Dictionary<string, string> { ["b.key"] = "2", ["a.key"] = "1" }
        };

        var saved = ResultAssert.Ok(await Svc.SetManyAsync(_tenant.Id, request));

        Assert.Equal(new[] { "a.key", "b.key" }, saved.Select(s => s.Key));
        Assert.Equal(2, _env.EventsOf<TenantSettingChangedEvent>().Count);
    }

    [Fact]
    public async Task SetMany_RejectsNothingToSet_NullValues_AndDuplicateKeys()
    {
        ResultAssert.Fails(
            await Svc.SetManyAsync(_tenant.Id, new SetTenantSettingsRequest()), TenantErrorKind.Validation);

        ResultAssert.Fails(
            await Svc.SetManyAsync(_tenant.Id, new SetTenantSettingsRequest
            {
                Values = new Dictionary<string, string> { ["a.key"] = null! }
            }), TenantErrorKind.Validation);

        ResultAssert.Fails(
            await Svc.SetManyAsync(_tenant.Id, new SetTenantSettingsRequest
            {
                Values = new Dictionary<string, string> { ["A.key"] = "1", ["a.KEY"] = "2" }
            }), TenantErrorKind.Validation);
    }

    [Fact]
    public async Task SetMany_EnforcesTheSettingLimit_ButStillAllowsUpdates()
    {
        _env.Config.MaxSettingsPerTenant = 2;
        _env.AddSetting(_tenant.Id, "one", "1");
        _env.AddSetting(_tenant.Id, "two", "2");

        ResultAssert.Fails(await Svc.SetAsync(_tenant.Id, "three", Value("3")), TenantErrorKind.Conflict);
        ResultAssert.Ok(await Svc.SetAsync(_tenant.Id, "two", Value("changed")));
    }

    [Fact]
    public async Task Writes_AreRefusedForUnknownAndArchivedTenants()
    {
        ResultAssert.Fails(await Svc.SetAsync(Guid.NewGuid(), "k", Value("v")), TenantErrorKind.NotFound);

        var archived = _env.AddTenant("Old", "old", TenantStatus.Archived);
        _env.AddSetting(archived.Id, "k", "v");

        ResultAssert.Fails(await Svc.SetAsync(archived.Id, "k", Value("x")), TenantErrorKind.Conflict, "tenant.read_only");
        ResultAssert.Fails(await Svc.RemoveAsync(archived.Id, "k"), TenantErrorKind.Conflict, "tenant.read_only");
    }

    [Fact]
    public async Task Reads_StillWorkForArchivedTenants()
    {
        var archived = _env.AddTenant("Old", "old", TenantStatus.Archived);
        _env.AddSetting(archived.Id, "k", "v");

        Assert.Single(ResultAssert.Ok(await Svc.ListAsync(archived.Id)));
        Assert.Equal("v", ResultAssert.Ok(await Svc.GetAsync(archived.Id, "k")).Value);
    }

    [Fact]
    public async Task List_IsOrderedByKey()
    {
        _env.AddSetting(_tenant.Id, "b", "2");
        _env.AddSetting(_tenant.Id, "a", "1");

        var list = ResultAssert.Ok(await Svc.ListAsync(_tenant.Id));

        Assert.Equal(new[] { "a", "b" }, list.Select(s => s.Key));
    }

    [Fact]
    public async Task Get_MissingSettingAndInvalidKeyAndUnknownTenant()
    {
        ResultAssert.Fails(await Svc.GetAsync(_tenant.Id, "missing"), TenantErrorKind.NotFound);
        ResultAssert.Fails(await Svc.GetAsync(_tenant.Id, "bad key"), TenantErrorKind.Validation);
        ResultAssert.Fails(await Svc.GetAsync(Guid.NewGuid(), "k"), TenantErrorKind.NotFound);
        ResultAssert.Fails(await Svc.ListAsync(Guid.NewGuid()), TenantErrorKind.NotFound);
    }

    [Fact]
    public async Task Get_NormalizesTheKey()
    {
        _env.AddSetting(_tenant.Id, "general.timezone", "UTC");

        Assert.Equal("UTC", ResultAssert.Ok(await Svc.GetAsync(_tenant.Id, " General.TimeZone ")).Value);
    }

    [Fact]
    public async Task Remove_DeletesTheSetting()
    {
        _env.AddSetting(_tenant.Id, "general.timezone", "UTC");

        ResultAssert.Ok(await Svc.RemoveAsync(_tenant.Id, "General.TimeZone"));

        Assert.Empty(_env.Read(db => db.Settings.ToList()));
        Assert.True(Assert.Single(_env.EventsOf<TenantSettingChangedEvent>()).Removed);
    }

    [Fact]
    public async Task Remove_MissingSettingIsNotFound()
        => ResultAssert.Fails(await Svc.RemoveAsync(_tenant.Id, "missing"), TenantErrorKind.NotFound);

    [Fact]
    public async Task Settings_BelongToTheirOwnTenant()
    {
        var other = _env.AddTenant("Other", "other");
        _env.AddSetting(other.Id, "secret", "other-value");

        ResultAssert.Fails(await Svc.GetAsync(_tenant.Id, "secret"), TenantErrorKind.NotFound);
        Assert.Empty(ResultAssert.Ok(await Svc.ListAsync(_tenant.Id)));
    }

    [Fact]
    public async Task BoundCallers_ReachOnlyTheirOwnTenantsSettings()
    {
        var other = _env.AddTenant("Other", "other");
        _env.Actor.IsPlatformAdmin = false;
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        ResultAssert.Ok(await Svc.SetAsync(_tenant.Id, "k", Value("v")));
        ResultAssert.Fails(await Svc.SetAsync(other.Id, "k", Value("v")), TenantErrorKind.Forbidden);
        ResultAssert.Fails(await Svc.ListAsync(other.Id), TenantErrorKind.Forbidden);
        ResultAssert.Fails(await Svc.RemoveAsync(other.Id, "k"), TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task Audit_NamesTheKeysButNeverTheValues()
    {
        await Svc.SetAsync(_tenant.Id, "payments.api_key", Value("super-secret-value"));

        var entry = _env.Read(db => db.AuditEntries.Single(a => a.Action == TenantAuditActions.SettingsUpdated));
        Assert.Contains("payments.api_key", entry.Details);
        Assert.DoesNotContain("super-secret-value", entry.Details);
    }
}

public sealed class TenantMembershipServiceTests : IDisposable
{
    private readonly TestEnv _env = new();
    private readonly TenantEntity _tenant;

    public TenantMembershipServiceTests() => _tenant = _env.AddTenant("Acme", "acme");

    public void Dispose() => _env.Dispose();

    private TenantMembershipService Svc => _env.CreateMembershipService();

    [Fact]
    public async Task Add_CreatesAMember_AndTheFirstOneIsTheDefault()
    {
        var user = Guid.NewGuid();

        var dto = ResultAssert.Ok(await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = user }));

        Assert.Equal(user, dto.UserId);
        Assert.False(dto.IsOwner);
        Assert.True(dto.IsDefault);
        Assert.Equal(_env.UtcNow, dto.JoinedAt);
        Assert.Single(_env.EventsOf<TenantMemberAddedEvent>());
    }

    [Fact]
    public async Task Add_SecondTenantDoesNotStealTheDefault()
    {
        var user = Guid.NewGuid();
        var second = _env.AddTenant("Second", "second");

        await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = user });
        var dto = ResultAssert.Ok(await Svc.AddMemberAsync(second.Id, new AddTenantMemberRequest { UserId = user }));

        Assert.False(dto.IsDefault);
    }

    [Fact]
    public async Task Add_RejectsDuplicates_EmptyUserIds_UnknownAndArchivedTenants()
    {
        var user = Guid.NewGuid();
        await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = user });
        var archived = _env.AddTenant("Old", "old", TenantStatus.Archived);

        ResultAssert.Fails(
            await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = user }), TenantErrorKind.Conflict);
        ResultAssert.Fails(
            await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = Guid.Empty }), TenantErrorKind.Validation);
        ResultAssert.Fails(
            await Svc.AddMemberAsync(Guid.NewGuid(), new AddTenantMemberRequest { UserId = Guid.NewGuid() }), TenantErrorKind.NotFound);
        ResultAssert.Fails(
            await Svc.AddMemberAsync(archived.Id, new AddTenantMemberRequest { UserId = Guid.NewGuid() }),
            TenantErrorKind.Conflict, "tenant.read_only");
    }

    [Fact]
    public async Task List_ReturnsMembersOldestFirst()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _env.AddMember(_tenant.Id, second, joinedAt: _env.UtcNow.AddDays(1));
        _env.AddMember(_tenant.Id, first, joinedAt: _env.UtcNow);

        var members = ResultAssert.Ok(await Svc.ListMembersAsync(_tenant.Id));

        Assert.Equal(new[] { first, second }, members.Select(m => m.UserId));
        ResultAssert.Fails(await Svc.ListMembersAsync(Guid.NewGuid()), TenantErrorKind.NotFound);
    }

    [Fact]
    public async Task Remove_DeletesTheMember()
    {
        var owner = Guid.NewGuid();
        var user = Guid.NewGuid();
        _env.AddMember(_tenant.Id, owner, isOwner: true);
        _env.AddMember(_tenant.Id, user);

        ResultAssert.Ok(await Svc.RemoveMemberAsync(_tenant.Id, user));

        Assert.Single(_env.Read(db => db.Members.ToList()));
        Assert.Single(_env.EventsOf<TenantMemberRemovedEvent>());
    }

    [Fact]
    public async Task Remove_RefusesTheLastOwner_ButNotWhenAnotherOwnerExists()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _env.AddMember(_tenant.Id, a, isOwner: true);

        ResultAssert.Fails(await Svc.RemoveMemberAsync(_tenant.Id, a), TenantErrorKind.Conflict);

        _env.AddMember(_tenant.Id, b, isOwner: true);

        ResultAssert.Ok(await Svc.RemoveMemberAsync(_tenant.Id, a));
    }

    [Fact]
    public async Task Remove_MovesTheDefaultToAnotherTenant()
    {
        var user = Guid.NewGuid();
        var other = _env.AddTenant("Other", "other");
        _env.AddMember(_tenant.Id, Guid.NewGuid(), isOwner: true);
        _env.AddMember(_tenant.Id, user, isDefault: true, joinedAt: _env.UtcNow);
        _env.AddMember(other.Id, user, joinedAt: _env.UtcNow.AddDays(1));

        ResultAssert.Ok(await Svc.RemoveMemberAsync(_tenant.Id, user));

        var remaining = _env.Read(db => db.Members.AsNoTracking().Single(m => m.UserId == user));
        Assert.Equal(other.Id, remaining.TenantId);
        Assert.True(remaining.IsDefault);
    }

    [Fact]
    public async Task Remove_UnknownMemberIsNotFound()
        => ResultAssert.Fails(await Svc.RemoveMemberAsync(_tenant.Id, Guid.NewGuid()), TenantErrorKind.NotFound);

    [Fact]
    public async Task SetOwner_PromotesAndDemotes()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _env.AddMember(_tenant.Id, a, isOwner: true);
        _env.AddMember(_tenant.Id, b);

        Assert.True(ResultAssert.Ok(await Svc.SetOwnerAsync(_tenant.Id, b, new SetTenantOwnerRequest { IsOwner = true })).IsOwner);
        Assert.False(ResultAssert.Ok(await Svc.SetOwnerAsync(_tenant.Id, a, new SetTenantOwnerRequest { IsOwner = false })).IsOwner);
    }

    [Fact]
    public async Task SetOwner_RefusesToDemoteTheLastOwner()
    {
        var a = Guid.NewGuid();
        _env.AddMember(_tenant.Id, a, isOwner: true);

        ResultAssert.Fails(
            await Svc.SetOwnerAsync(_tenant.Id, a, new SetTenantOwnerRequest { IsOwner = false }), TenantErrorKind.Conflict);
        Assert.True(_env.Read(db => db.Members.Single()).IsOwner);
    }

    [Fact]
    public async Task SetOwner_NoChangeIsASilentSuccess_AndUnknownMemberIsNotFound()
    {
        var a = Guid.NewGuid();
        _env.AddMember(_tenant.Id, a, isOwner: true);

        ResultAssert.Ok(await Svc.SetOwnerAsync(_tenant.Id, a, new SetTenantOwnerRequest { IsOwner = true }));
        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));

        ResultAssert.Fails(
            await Svc.SetOwnerAsync(_tenant.Id, Guid.NewGuid(), new SetTenantOwnerRequest { IsOwner = true }),
            TenantErrorKind.NotFound);
    }

    [Fact]
    public async Task ListForUser_ShowsTheirTenantsWithStatusAndDefault()
    {
        var user = Guid.NewGuid();
        var other = _env.AddTenant("Other", "other", TenantStatus.Suspended);
        _env.AddMember(_tenant.Id, user, isOwner: true, isDefault: true, joinedAt: _env.UtcNow);
        _env.AddMember(other.Id, user, joinedAt: _env.UtcNow.AddDays(1));

        var list = ResultAssert.Ok(await Svc.ListForUserAsync(user));

        Assert.Equal(new[] { "acme", "other" }, list.Select(m => m.Slug));
        Assert.True(list[0].IsOwner && list[0].IsDefault);
        Assert.Equal(TenantStatus.Suspended, list[1].Status);
    }

    [Fact]
    public async Task ListForUser_IsForTheUserThemselvesOrPlatformOnly()
    {
        var user = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;

        ResultAssert.Fails(await Svc.ListForUserAsync(user), TenantErrorKind.Forbidden);

        _env.Actor.UserId = user;
        ResultAssert.Ok(await Svc.ListForUserAsync(user));
        ResultAssert.Fails(await Svc.ListForUserAsync(Guid.NewGuid()), TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task SetDefault_MovesTheDefaultFlag()
    {
        var user = Guid.NewGuid();
        var other = _env.AddTenant("Other", "other");
        _env.AddMember(_tenant.Id, user, isDefault: true);
        _env.AddMember(other.Id, user);

        ResultAssert.Ok(await Svc.SetDefaultTenantAsync(user, other.Id));

        var members = _env.Read(db => db.Members.AsNoTracking().Where(m => m.UserId == user).ToList());
        Assert.True(members.Single(m => m.TenantId == other.Id).IsDefault);
        Assert.False(members.Single(m => m.TenantId == _tenant.Id).IsDefault);
    }

    [Fact]
    public async Task SetDefault_NeedsMembership_AndIsSelfOrPlatformOnly()
    {
        var user = Guid.NewGuid();

        ResultAssert.Fails(await Svc.SetDefaultTenantAsync(user, _tenant.Id), TenantErrorKind.NotFound);

        _env.Actor.IsPlatformAdmin = false;
        ResultAssert.Fails(await Svc.SetDefaultTenantAsync(user, _tenant.Id), TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task IsMember_ReportsMembership()
    {
        var user = Guid.NewGuid();
        _env.AddMember(_tenant.Id, user);

        Assert.True(await Svc.IsMemberAsync(_tenant.Id, user));
        Assert.False(await Svc.IsMemberAsync(_tenant.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task BoundCallers_ReachOnlyTheirOwnTenantsMembers()
    {
        var other = _env.AddTenant("Other", "other");
        _env.Actor.IsPlatformAdmin = false;
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        ResultAssert.Ok(await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = Guid.NewGuid() }));
        ResultAssert.Fails(
            await Svc.AddMemberAsync(other.Id, new AddTenantMemberRequest { UserId = Guid.NewGuid() }), TenantErrorKind.Forbidden);
        ResultAssert.Fails(await Svc.ListMembersAsync(other.Id), TenantErrorKind.Forbidden);
        ResultAssert.Fails(await Svc.RemoveMemberAsync(other.Id, Guid.NewGuid()), TenantErrorKind.Forbidden);
    }
}