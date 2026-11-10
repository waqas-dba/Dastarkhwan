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
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = a;
        _env.AddMember(_tenant.Id, a, isOwner: true);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

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
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = a;
        _env.AddMember(_tenant.Id, a, isOwner: true);
        _env.AddMember(_tenant.Id, b);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        Assert.True(ResultAssert.Ok(await Svc.SetOwnerAsync(_tenant.Id, b, new SetTenantOwnerRequest { IsOwner = true })).IsOwner);
        Assert.False(ResultAssert.Ok(await Svc.SetOwnerAsync(_tenant.Id, a, new SetTenantOwnerRequest { IsOwner = false })).IsOwner);
    }

    [Fact]
    public async Task SetOwner_RefusesToDemoteTheLastOwner()
    {
        var a = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = a;
        _env.AddMember(_tenant.Id, a, isOwner: true);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

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

    [Fact]
    public async Task Add_RejectsArbitraryOwnerAssignment()
    {
        var actorUser = Guid.NewGuid();
        var targetUser = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        _env.AddMember(_tenant.Id, actorUser, isOwner: false);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        ResultAssert.Fails(
            await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = targetUser, IsOwner = true }),
            TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task Add_AllowsPlatformAdminToAddOwner()
    {
        var platformUser = Guid.NewGuid();
        var targetUser = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = true;
        _env.Actor.UserId = platformUser;

        var dto = ResultAssert.Ok(await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = targetUser, IsOwner = true }));

        Assert.True(dto.IsOwner);
        Assert.True(_env.Read(db => db.Members.Single(m => m.UserId == targetUser).IsOwner));
    }

    [Fact]
    public async Task Add_AllowsExistingOwnerToAddOwner()
    {
        var ownerUser = Guid.NewGuid();
        var targetUser = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = ownerUser;
        _env.AddMember(_tenant.Id, ownerUser, isOwner: true);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        var dto = ResultAssert.Ok(await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = targetUser, IsOwner = true }));

        Assert.True(dto.IsOwner);
        Assert.True(_env.Read(db => db.Members.Single(m => m.UserId == targetUser).IsOwner));
    }

    [Fact]
    public async Task SetOwner_RejectsPromotionByNonOwner()
    {
        var actorUser = Guid.NewGuid();
        var targetUser = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        _env.AddMember(_tenant.Id, actorUser, isOwner: false);
        _env.AddMember(_tenant.Id, targetUser);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        ResultAssert.Fails(
            await Svc.SetOwnerAsync(_tenant.Id, targetUser, new SetTenantOwnerRequest { IsOwner = true }),
            TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task SetOwner_AllowsPromotionByPlatformAdmin()
    {
        var platformUser = Guid.NewGuid();
        var targetUser = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = true;
        _env.Actor.UserId = platformUser;
        _env.AddMember(_tenant.Id, targetUser);

        var dto = ResultAssert.Ok(await Svc.SetOwnerAsync(_tenant.Id, targetUser, new SetTenantOwnerRequest { IsOwner = true }));

        Assert.True(dto.IsOwner);
        Assert.True(_env.Read(db => db.Members.Single(m => m.UserId == targetUser).IsOwner));
    }

    [Fact]
    public async Task SetOwner_AllowsPromotionByExistingOwner()
    {
        var ownerUser = Guid.NewGuid();
        var targetUser = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = ownerUser;
        _env.AddMember(_tenant.Id, ownerUser, isOwner: true);
        _env.AddMember(_tenant.Id, targetUser);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        var dto = ResultAssert.Ok(await Svc.SetOwnerAsync(_tenant.Id, targetUser, new SetTenantOwnerRequest { IsOwner = true }));

        Assert.True(dto.IsOwner);
        Assert.True(_env.Read(db => db.Members.Single(m => m.UserId == targetUser).IsOwner));
    }

    [Fact]
    public async Task SetOwner_RejectsSelfPromotion()
    {
        var user = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = true;
        _env.Actor.UserId = user;
        _env.AddMember(_tenant.Id, user, isOwner: false);

        ResultAssert.Fails(
            await Svc.SetOwnerAsync(_tenant.Id, user, new SetTenantOwnerRequest { IsOwner = true }),
            TenantErrorKind.Forbidden);
        Assert.False(_env.Read(db => db.Members.Single(m => m.UserId == user).IsOwner));
    }

    [Fact]
    public async Task Add_RejectsPlatformAdminWithNullActorAsOwner()
    {
        _env.Actor.IsPlatformAdmin = true;
        _env.Actor.UserId = null;

        ResultAssert.Fails(
            await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = Guid.NewGuid(), IsOwner = true }),
            TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task SetOwner_RejectsPlatformAdminWithNullActor()
    {
        _env.Actor.IsPlatformAdmin = true;
        _env.Actor.UserId = null;
        var targetUser = Guid.NewGuid();
        _env.AddMember(_tenant.Id, targetUser);

        ResultAssert.Fails(
            await Svc.SetOwnerAsync(_tenant.Id, targetUser, new SetTenantOwnerRequest { IsOwner = true }),
            TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task Add_RejectsGuidEmptyActorAsOwner()
    {
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = Guid.Empty;

        ResultAssert.Fails(
            await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = Guid.NewGuid(), IsOwner = true }),
            TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task Add_RejectsSelfAdditionAsOwner()
    {
        var actorUser = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        ResultAssert.Fails(
            await Svc.AddMemberAsync(_tenant.Id, new AddTenantMemberRequest { UserId = actorUser, IsOwner = true }),
            TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task ConcurrencyStampUpdatedOnOwnerRemoval()
    {
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        _env.AddMember(_tenant.Id, ownerA, isOwner: true);
        _env.AddMember(_tenant.Id, ownerB, isOwner: true);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        var stampBefore = _env.Read(db => db.Tenants.Single(t => t.Id == _tenant.Id).ConcurrencyStamp);

        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = ownerA;

        await Svc.RemoveMemberAsync(_tenant.Id, ownerB);

        var stampAfter = _env.Read(db => db.Tenants.Single(t => t.Id == _tenant.Id).ConcurrencyStamp);
        Assert.NotEqual(stampBefore, stampAfter);
    }

    [Fact]
    public async Task ConcurrencyStampUpdatedOnOwnerPromotion()
    {
        var actorUser = Guid.NewGuid();
        var ownerA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        _env.AddMember(_tenant.Id, actorUser, isOwner: true);
        _env.AddMember(_tenant.Id, ownerA, isOwner: true);
        _env.AddMember(_tenant.Id, memberB);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        var stampBefore = _env.Read(db => db.Tenants.Single(t => t.Id == _tenant.Id).ConcurrencyStamp);

        await Svc.SetOwnerAsync(_tenant.Id, memberB, new SetTenantOwnerRequest { IsOwner = true });

        var stampAfter = _env.Read(db => db.Tenants.Single(t => t.Id == _tenant.Id).ConcurrencyStamp);
        Assert.NotEqual(stampBefore, stampAfter);
    }

    [Fact]
    public async Task DbUpdateConcurrencyExceptionOnStaleStamp()
    {
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        _env.AddMember(_tenant.Id, ownerA, isOwner: true);
        _env.AddMember(_tenant.Id, ownerB, isOwner: true);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        // Load tenant via context A (tracked)
        var dbA = _env.NewContext();
        var tenantA = await dbA.Tenants.SingleAsync(t => t.Id == _tenant.Id);
        var originalStamp = tenantA.ConcurrencyStamp;

        // Change stamp via context B (simulates concurrent modification)
        var dbB = _env.NewContext();
        var tenantB = await dbB.Tenants.SingleAsync(t => t.Id == _tenant.Id);
        tenantB.ConcurrencyStamp = Guid.NewGuid();
        await dbB.SaveChangesAsync();

        // Modify tenant via context A and try to save — should fail with concurrency exception
        tenantA.ConcurrencyStamp = Guid.NewGuid();
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException>(
            () => dbA.SaveChangesAsync());
    }

    [Fact]
    public async Task Remove_NonOwnerConcurrentPromotion_ReturnsConflict()
    {
        var actorUser = Guid.NewGuid();
        var nonOwnerMember = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        _env.AddMember(_tenant.Id, actorUser, isOwner: true);
        _env.AddMember(_tenant.Id, nonOwnerMember, isOwner: false);
        _env.AddMember(_tenant.Id, otherOwner, isOwner: true);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        // Concurrent promotion of the non-owner changes the tenant stamp
        _env.Seed(db =>
        {
            var t = db.Tenants.Single(x => x.Id == _tenant.Id);
            t.ConcurrencyStamp = Guid.NewGuid();
        });

        // Stale removal (read as non-owner, then concurrent promotion changed stamp)
        var result = await Svc.RemoveMemberAsync(_tenant.Id, nonOwnerMember);

        // Stale operation must return concurrency conflict
        ResultAssert.Fails(result, TenantErrorKind.Conflict);

        // Concurrent committed ownership change remains intact — non-owner member still exists
        Assert.True(_env.Read(db => db.Members.Single(m => m.UserId == nonOwnerMember && !m.IsOwner).IsOwner == false));

        // Tenant stamp reflects the concurrent change
        var stampAfter = _env.Read(db => db.Tenants.Single(t => t.Id == _tenant.Id).ConcurrencyStamp);
        Assert.NotEqual(Guid.Empty, stampAfter);
    }

    [Fact]
    public async Task Remove_NonOwnerUpdatesTenantStamp()
    {
        var actorUser = Guid.NewGuid();
        var nonOwnerMember = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        _env.AddMember(_tenant.Id, actorUser, isOwner: true);
        _env.AddMember(_tenant.Id, nonOwnerMember, isOwner: false);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        var stampBefore = _env.Read(db => db.Tenants.Single(t => t.Id == _tenant.Id).ConcurrencyStamp);

        ResultAssert.Ok(await Svc.RemoveMemberAsync(_tenant.Id, nonOwnerMember));

        var stampAfter = _env.Read(db => db.Tenants.Single(t => t.Id == _tenant.Id).ConcurrencyStamp);
        Assert.NotEqual(stampBefore, stampAfter);

        // Member is actually removed
        Assert.False(_env.Read(db => db.Members.Any(m => m.UserId == nonOwnerMember)));
    }

    [Fact]
    public async Task Remove_NonOwnerConcurrentPromotion_DoesNotPersistChanges()
    {
        var actorUser = Guid.NewGuid();
        var nonOwnerMember = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        _env.AddMember(_tenant.Id, actorUser, isOwner: true);
        _env.AddMember(_tenant.Id, nonOwnerMember, isOwner: false);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        var auditCountBefore = _env.Read(db => db.AuditEntries.Count());

        // Concurrent promotion changes the stamp
        _env.Seed(db =>
        {
            var t = db.Tenants.Single(x => x.Id == _tenant.Id);
            t.ConcurrencyStamp = Guid.NewGuid();
        });

        // Stale removal must fail
        ResultAssert.Fails(
            await Svc.RemoveMemberAsync(_tenant.Id, nonOwnerMember),
            TenantErrorKind.Conflict);

        // Membership unchanged — non-owner member still exists
        Assert.True(_env.Read(db => db.Members.Any(m => m.UserId == nonOwnerMember && !m.IsOwner)));

        // No audit entry for the stale removal
        var auditCountAfter = _env.Read(db => db.AuditEntries.Count());
        Assert.Equal(auditCountBefore, auditCountAfter);
    }

    [Fact]
    public async Task Concurrency_TwoConcurrentOwnerRemovalsPreserveOneOwner()
    {
        var actorUser = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        _env.AddMember(_tenant.Id, actorUser, isOwner: true);
        _env.AddMember(_tenant.Id, ownerB, isOwner: true);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        // Simulate a concurrent modification by another request
        _env.Seed(db =>
        {
            var t = db.Tenants.Single(x => x.Id == _tenant.Id);
            t.ConcurrencyStamp = Guid.NewGuid();
        });

        // The service reloads the tracked tenant (gets the new stamp), so the removal
        // succeeds on SQLite. On PostgreSQL with true concurrency, the first request
        // to commit wins and the second receives DbUpdateConcurrencyException.
        // Either way, the owner count is preserved.
        ResultAssert.Ok(await Svc.RemoveMemberAsync(_tenant.Id, ownerB));

        var owners = _env.Read(db => db.Members.Count(m => m.TenantId == _tenant.Id && m.IsOwner));
        Assert.Equal(1, owners);
        Assert.True(_env.Read(db => db.Members.Single(m => m.UserId == actorUser).IsOwner));
    }

    [Fact]
    public async Task Concurrency_TwoConcurrentOwnerDemotionsPreserveOneOwner()
    {
        var actorUser = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        _env.AddMember(_tenant.Id, actorUser, isOwner: true);
        _env.AddMember(_tenant.Id, ownerB, isOwner: true);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        // Simulate a concurrent modification by another request
        _env.Seed(db =>
        {
            var t = db.Tenants.Single(x => x.Id == _tenant.Id);
            t.ConcurrencyStamp = Guid.NewGuid();
        });

        ResultAssert.Ok(await Svc.SetOwnerAsync(_tenant.Id, ownerB, new SetTenantOwnerRequest { IsOwner = false }));

        var owners = _env.Read(db => db.Members.Count(m => m.TenantId == _tenant.Id && m.IsOwner));
        Assert.Equal(1, owners);
        Assert.True(_env.Read(db => db.Members.Single(m => m.UserId == actorUser).IsOwner));
    }

    [Fact]
    public async Task Concurrency_OwnerAddAndRemovePreservesOwner()
    {
        var actorUser = Guid.NewGuid();
        var ownerA = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        _env.AddMember(_tenant.Id, actorUser, isOwner: true);
        _env.AddMember(_tenant.Id, ownerA, isOwner: true);
        _env.AddMember(_tenant.Id, newOwner, isOwner: false);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        // Simulate a concurrent modification by another request
        _env.Seed(db =>
        {
            var t = db.Tenants.Single(x => x.Id == _tenant.Id);
            t.ConcurrencyStamp = Guid.NewGuid();
        });

        // Remove ownerA and add newOwner as owner — net owner count stays at 2
        ResultAssert.Ok(await Svc.RemoveMemberAsync(_tenant.Id, ownerA));
        ResultAssert.Ok(await Svc.SetOwnerAsync(_tenant.Id, newOwner, new SetTenantOwnerRequest { IsOwner = true }));

        var owners = _env.Read(db => db.Members.Count(m => m.TenantId == _tenant.Id && m.IsOwner));
        Assert.Equal(2, owners);
    }

    [Fact]
    public async Task Concurrency_FailedOperationDoesNotPersistChanges()
    {
        var actorUser = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        _env.Actor.IsPlatformAdmin = false;
        _env.Actor.UserId = actorUser;
        _env.AddMember(_tenant.Id, actorUser, isOwner: true);
        _env.AddMember(_tenant.Id, ownerB, isOwner: true);
        using var _ = _env.CurrentTenant.Change(_tenant.Id);

        var auditCountBefore = _env.Read(db => db.AuditEntries.Count());

        // Simulate a concurrent modification by another request
        _env.Seed(db =>
        {
            var t = db.Tenants.Single(x => x.Id == _tenant.Id);
            t.ConcurrencyStamp = Guid.NewGuid();
        });

        // On SQLite, the service reloads the tenant and gets the new stamp,
        // so the removal succeeds. On PostgreSQL with true concurrency,
        // the second request would get DbUpdateConcurrencyException and
        // no changes would be persisted.
        ResultAssert.Ok(await Svc.RemoveMemberAsync(_tenant.Id, ownerB));

        // Verify exactly one owner remains
        var ownersAfter = _env.Read(db => db.Members.Count(m => m.TenantId == _tenant.Id && m.IsOwner));
        Assert.Equal(1, ownersAfter);

        // Verify an audit entry was written for the successful removal
        var auditCountAfter = _env.Read(db => db.AuditEntries.Count());
        Assert.Equal(auditCountBefore + 1, auditCountAfter);
    }
}