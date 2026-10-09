namespace CoreKit.Tenant.Tests;

public sealed class TenantServiceCreateTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private TenantService Svc => _env.CreateTenantService();

    [Fact]
    public async Task Create_ActivatesByDefault_AndBuildsTheSlugFromTheName()
    {
        var dto = ResultAssert.Ok(await Svc.CreateAsync(new CreateTenantRequest { Name = "  Acme Foods " }));

        Assert.Equal("Acme Foods", dto.Name);
        Assert.Equal("acme-foods", dto.Slug);
        Assert.Equal(TenantStatus.Active, dto.Status);
        Assert.Equal(_env.UtcNow, dto.CreatedAt);

        var row = _env.Read(db => db.Tenants.AsNoTracking().Single(t => t.Id == dto.Id));
        Assert.Equal("acme-foods", row.Slug);
        Assert.NotEqual(Guid.Empty, row.ConcurrencyStamp);
    }

    [Fact]
    public async Task Create_CanStartPending()
    {
        var dto = ResultAssert.Ok(await Svc.CreateAsync(
            new CreateTenantRequest { Name = "Acme", ActivateImmediately = false }));

        Assert.Equal(TenantStatus.Pending, dto.Status);
    }

    [Fact]
    public async Task Create_NormalizesAnExplicitSlug()
    {
        var dto = ResultAssert.Ok(await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme", Slug = "  My-Shop " }));

        Assert.Equal("my-shop", dto.Slug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RequiresAName(string? name)
        => ResultAssert.Fails(await Svc.CreateAsync(new CreateTenantRequest { Name = name! }), TenantErrorKind.Validation);

    [Fact]
    public async Task Create_RejectsATooLongName()
        => ResultAssert.Fails(
            await Svc.CreateAsync(new CreateTenantRequest { Name = new string('a', 201) }), TenantErrorKind.Validation);

    [Theory]
    [InlineData("ab")]
    [InlineData("-abc")]
    [InlineData("ab--cd")]
    [InlineData("has space")]
    [InlineData("under_score")]
    public async Task Create_RejectsAnInvalidSlug(string slug)
        => ResultAssert.Fails(
            await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme", Slug = slug }), TenantErrorKind.Validation);

    [Fact]
    public async Task Create_RejectsAReservedSlug()
        => ResultAssert.Fails(
            await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme", Slug = "admin" }), TenantErrorKind.Validation);

    [Fact]
    public async Task Create_RejectsConfiguredReservedSlugs()
    {
        _env.Config.AdditionalReservedSlugs.Add("shop");

        ResultAssert.Fails(
            await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme", Slug = "SHOP" }), TenantErrorKind.Validation);
    }

    [Fact]
    public async Task Create_RejectsADuplicateSlug_IgnoringCase()
    {
        _env.AddTenant("Acme", "acme");

        ResultAssert.Fails(
            await Svc.CreateAsync(new CreateTenantRequest { Name = "Other", Slug = "ACME" }),
            TenantErrorKind.Conflict);
    }

    [Fact]
    public async Task Create_GeneratedSlugsGetASuffixWhenTaken()
    {
        _env.AddTenant("Acme Foods", "acme-foods");
        _env.AddTenant("Acme Foods 2", "acme-foods-2");

        var dto = ResultAssert.Ok(await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme Foods" }));

        Assert.Equal("acme-foods-3", dto.Slug);
    }

    [Fact]
    public async Task Create_GeneratedSlugsSkipReservedNames()
    {
        var dto = ResultAssert.Ok(await Svc.CreateAsync(new CreateTenantRequest { Name = "Admin" }));

        Assert.Equal("admin-2", dto.Slug);
    }

    [Theory]
    [InlineData("ab", "tenant-ab")]
    [InlineData("\u0627\u0631\u062f\u0648", "tenant")]
    [InlineData("!!!", "tenant")]
    public async Task Create_GeneratesAFallbackSlugForUnusableNames(string name, string expected)
    {
        var dto = ResultAssert.Ok(await Svc.CreateAsync(new CreateTenantRequest { Name = name }));

        Assert.Equal(expected, dto.Slug);
    }

    [Fact]
    public async Task Create_WithOwner_AddsThemAsOwnerAndDefault()
    {
        var owner = Guid.NewGuid();

        var dto = ResultAssert.Ok(await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme", OwnerUserId = owner }));

        var member = _env.Read(db => db.Members.AsNoTracking().Single(m => m.TenantId == dto.Id));
        Assert.Equal(owner, member.UserId);
        Assert.True(member.IsOwner);
        Assert.True(member.IsDefault);
        Assert.Single(_env.EventsOf<TenantMemberAddedEvent>());
    }

    [Fact]
    public async Task Create_WithOwner_KeepsAnExistingDefault()
    {
        var owner = Guid.NewGuid();
        var first = ResultAssert.Ok(await Svc.CreateAsync(new CreateTenantRequest { Name = "First", OwnerUserId = owner }));

        var second = ResultAssert.Ok(await Svc.CreateAsync(new CreateTenantRequest { Name = "Second", OwnerUserId = owner }));

        var members = _env.Read(db => db.Members.AsNoTracking().Where(m => m.UserId == owner).ToList());
        Assert.True(members.Single(m => m.TenantId == first.Id).IsDefault);
        Assert.False(members.Single(m => m.TenantId == second.Id).IsDefault);
    }

    [Fact]
    public async Task Create_RejectsAnEmptyOwnerId()
        => ResultAssert.Fails(
            await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme", OwnerUserId = Guid.Empty }),
            TenantErrorKind.Validation);

    [Fact]
    public async Task Create_IsPlatformOnly_ForTenantBoundCallers()
    {
        using var _ = _env.CurrentTenant.Change(Guid.NewGuid());

        ResultAssert.Fails(
            await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme" }), TenantErrorKind.Forbidden, "tenant.platform_only");
    }

    [Fact]
    public async Task Create_IsRefusedWithoutPlatformAccess_ButWorksForGrantedCode()
    {
        _env.Actor.IsPlatformAdmin = false;

        ResultAssert.Fails(
            await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme" }), TenantErrorKind.Forbidden);

        using (_env.PlatformAccess.Grant())
            ResultAssert.Ok(await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme" }));
    }

    [Fact]
    public async Task Create_WritesAnAuditEntryWithTheActor_AndPublishesAnEvent()
    {
        var actor = Guid.NewGuid();
        _env.Actor.UserId = actor;

        var dto = ResultAssert.Ok(await Svc.CreateAsync(new CreateTenantRequest { Name = "Acme" }));

        var entry = _env.Read(db => db.AuditEntries.AsNoTracking().Single(a => a.TenantId == dto.Id));
        Assert.Equal(TenantAuditActions.Created, entry.Action);
        Assert.Equal(actor, entry.ActorUserId);
        Assert.Equal(_env.UtcNow, entry.OccurredAt);

        var created = Assert.Single(_env.EventsOf<TenantCreatedEvent>());
        Assert.Equal(dto.Id, created.TenantId);
        Assert.Equal("acme", created.Slug);
    }

    [Fact]
    public async Task Create_FailureLeavesNoAuditOrEvent()
    {
        _env.AddTenant("Acme", "acme");

        await Svc.CreateAsync(new CreateTenantRequest { Name = "Other", Slug = "acme" });

        Assert.Empty(_env.EventsOf<TenantCreatedEvent>());
        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
    }
}

public sealed class TenantServiceUpdateTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private TenantService Svc => _env.CreateTenantService();

    [Fact]
    public async Task Update_ChangesTheName()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        var before = _env.Read(db => db.Tenants.Single(t => t.Id == tenant.Id)).ConcurrencyStamp;

        var dto = ResultAssert.Ok(await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Name = "  Acme Ltd " }));

        Assert.Equal("Acme Ltd", dto.Name);
        Assert.Equal(_env.UtcNow, dto.UpdatedAt);

        var row = _env.Read(db => db.Tenants.AsNoTracking().Single(t => t.Id == tenant.Id));
        Assert.Equal("Acme Ltd", row.Name);
        Assert.NotEqual(before, row.ConcurrencyStamp);
        Assert.Single(_env.EventsOf<TenantUpdatedEvent>());
    }

    [Fact]
    public async Task Update_PlatformCanChangeTheSlug()
    {
        var tenant = _env.AddTenant("Acme", "acme");

        var dto = ResultAssert.Ok(await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Slug = " New-Slug " }));

        Assert.Equal("new-slug", dto.Slug);
    }

    [Fact]
    public async Task Update_TenantMayRenameItselfButNotChangeItsSlug()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        _env.Actor.IsPlatformAdmin = false;
        using var _ = _env.CurrentTenant.Change(tenant.Id);

        ResultAssert.Ok(await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Name = "Renamed" }));
        ResultAssert.Fails(
            await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Slug = "other" }),
            TenantErrorKind.Forbidden, "tenant.platform_only");
    }

    [Fact]
    public async Task Update_RefusesAnotherTenant()
    {
        var own = _env.AddTenant("Own", "own");
        var other = _env.AddTenant("Other", "other");
        using var _ = _env.CurrentTenant.Change(own.Id);

        ResultAssert.Fails(
            await Svc.UpdateAsync(other.Id, new UpdateTenantRequest { Name = "Hacked" }),
            TenantErrorKind.Forbidden, "tenant.cross_tenant_access");
        Assert.Equal("Other", _env.Read(db => db.Tenants.Single(t => t.Id == other.Id)).Name);
    }

    [Fact]
    public async Task Update_UnknownTenant_IsNotFound()
        => ResultAssert.Fails(
            await Svc.UpdateAsync(Guid.NewGuid(), new UpdateTenantRequest { Name = "x" }), TenantErrorKind.NotFound);

    [Fact]
    public async Task Update_ArchivedTenantIsReadOnly()
    {
        var tenant = _env.AddTenant("Acme", "acme", TenantStatus.Archived);

        ResultAssert.Fails(
            await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Name = "x" }),
            TenantErrorKind.Conflict, "tenant.read_only");
    }

    [Fact]
    public async Task Update_RejectsAnInvalidName_AndAnInvalidSlug()
    {
        var tenant = _env.AddTenant("Acme", "acme");

        ResultAssert.Fails(
            await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Name = "  " }), TenantErrorKind.Validation);
        ResultAssert.Fails(
            await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Slug = "a" }), TenantErrorKind.Validation);
        ResultAssert.Fails(
            await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Slug = "admin" }), TenantErrorKind.Validation);
    }

    [Fact]
    public async Task Update_RejectsADuplicateSlug()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        _env.AddTenant("Other", "other");

        ResultAssert.Fails(
            await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Slug = "OTHER" }), TenantErrorKind.Conflict);
    }

    [Fact]
    public async Task Update_RefusedRequestChangesNothing()
    {
        var tenant = _env.AddTenant("Acme", "acme");

        // The name is fine but the slug is not: the name must not be saved either.
        await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Name = "Renamed", Slug = "a" });

        Assert.Equal("Acme", _env.Read(db => db.Tenants.Single(t => t.Id == tenant.Id)).Name);
    }

    [Fact]
    public async Task Update_NothingChanged_IsASilentSuccess()
    {
        var tenant = _env.AddTenant("Acme", "acme");

        var dto = ResultAssert.Ok(await Svc.UpdateAsync(
            tenant.Id, new UpdateTenantRequest { Name = "Acme", Slug = "acme" }));

        Assert.Null(dto.UpdatedAt);
        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
        Assert.Empty(_env.EventsOf<TenantUpdatedEvent>());
    }

    [Fact]
    public async Task Update_AuditsWhatChanged()
    {
        var tenant = _env.AddTenant("Acme", "acme");

        await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Name = "Acme Ltd" });

        var entry = _env.Read(db => db.AuditEntries.Single(a => a.Action == TenantAuditActions.Updated));
        Assert.Contains("Acme -> Acme Ltd", entry.Details);
    }

    [Fact]
    public async Task Update_ForgetsTheCachedTenant()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        await _env.InfoProvider.GetByIdAsync(tenant.Id);
        await _env.InfoProvider.GetBySlugAsync("acme");

        await Svc.UpdateAsync(tenant.Id, new UpdateTenantRequest { Name = "Acme Ltd", Slug = "acme-ltd" });

        Assert.Equal("Acme Ltd", (await _env.InfoProvider.GetByIdAsync(tenant.Id))!.Name);
        Assert.Null(await _env.InfoProvider.GetBySlugAsync("acme"));
        Assert.NotNull(await _env.InfoProvider.GetBySlugAsync("acme-ltd"));
    }
}

public sealed class TenantServiceStatusTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private TenantService Svc => _env.CreateTenantService();

    [Fact]
    public async Task Suspend_ActiveTenant_RecordsReasonTimeAuditAndEvent()
    {
        var tenant = _env.AddTenant();
        _env.Time.Advance(TimeSpan.FromHours(1));

        var dto = ResultAssert.Ok(await Svc.SuspendAsync(tenant.Id, new ChangeTenantStatusRequest { Reason = " unpaid " }));

        Assert.Equal(TenantStatus.Suspended, dto.Status);
        Assert.Equal("unpaid", dto.StatusReason);
        Assert.Equal(_env.UtcNow, dto.StatusChangedAt);

        var entry = _env.Read(db => db.AuditEntries.Single(a => a.Action == TenantAuditActions.Suspended));
        Assert.Contains("Active -> Suspended", entry.Details);
        Assert.Contains("unpaid", entry.Details);

        var changed = Assert.Single(_env.EventsOf<TenantStatusChangedEvent>());
        Assert.Equal(TenantStatus.Active, changed.From);
        Assert.Equal(TenantStatus.Suspended, changed.To);
        Assert.Equal("unpaid", changed.Reason);
    }

    [Theory]
    [InlineData(TenantStatus.Pending)]
    [InlineData(TenantStatus.Suspended)]
    public async Task Activate_FromPendingOrSuspended_ClearsTheReason(TenantStatus from)
    {
        var tenant = _env.AddTenant(status: from, reason: "old reason");

        var dto = ResultAssert.Ok(await Svc.ActivateAsync(tenant.Id));

        Assert.Equal(TenantStatus.Active, dto.Status);
        Assert.Null(dto.StatusReason);
    }

    [Theory]
    [InlineData(TenantStatus.Pending)]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Suspended)]
    public async Task Archive_FromAnyOpenStatus(TenantStatus from)
    {
        var tenant = _env.AddTenant(status: from);

        var dto = ResultAssert.Ok(await Svc.ArchiveAsync(tenant.Id, new ChangeTenantStatusRequest { Reason = "closed" }));

        Assert.Equal(TenantStatus.Archived, dto.Status);
        Assert.Equal("closed", dto.StatusReason);
    }

    [Fact]
    public async Task Restore_MovesAnArchivedTenantToSuspended_NotActive()
    {
        var tenant = _env.AddTenant(status: TenantStatus.Archived);

        var dto = ResultAssert.Ok(await Svc.RestoreAsync(tenant.Id));

        Assert.Equal(TenantStatus.Suspended, dto.Status);
        Assert.Contains(_env.Read(db => db.AuditEntries.ToList()), a => a.Action == TenantAuditActions.Restored);
    }

    [Theory]
    [InlineData(TenantStatus.Pending)]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Suspended)]
    public async Task Restore_OnlyWorksOnArchivedTenants(TenantStatus from)
    {
        var tenant = _env.AddTenant(status: from);

        ResultAssert.Fails(await Svc.RestoreAsync(tenant.Id), TenantErrorKind.Conflict, "tenant.invalid_transition");
    }

    [Fact]
    public async Task InvalidTransitions_AreConflicts_AndChangeNothing()
    {
        var active = _env.AddTenant("A One", "a-one", TenantStatus.Active);
        var pending = _env.AddTenant("P One", "p-one", TenantStatus.Pending);
        var archived = _env.AddTenant("Z One", "z-one", TenantStatus.Archived);

        ResultAssert.Fails(await Svc.ActivateAsync(active.Id), TenantErrorKind.Conflict, "tenant.invalid_transition");
        ResultAssert.Fails(await Svc.SuspendAsync(pending.Id, new ChangeTenantStatusRequest()), TenantErrorKind.Conflict);
        ResultAssert.Fails(await Svc.SuspendAsync(archived.Id, new ChangeTenantStatusRequest()), TenantErrorKind.Conflict);
        ResultAssert.Fails(await Svc.ArchiveAsync(archived.Id, new ChangeTenantStatusRequest()), TenantErrorKind.Conflict);
        ResultAssert.Fails(await Svc.ActivateAsync(archived.Id), TenantErrorKind.Conflict);

        Assert.Empty(_env.Read(db => db.AuditEntries.ToList()));
        Assert.Empty(_env.Published);
    }

    [Fact]
    public async Task StatusChanges_RejectAReasonThatIsTooLong()
    {
        var tenant = _env.AddTenant();

        ResultAssert.Fails(
            await Svc.SuspendAsync(tenant.Id, new ChangeTenantStatusRequest { Reason = new string('x', 501) }),
            TenantErrorKind.Validation);
    }

    [Fact]
    public async Task StatusChanges_UnknownTenantIsNotFound()
        => ResultAssert.Fails(await Svc.ActivateAsync(Guid.NewGuid()), TenantErrorKind.NotFound);

    [Fact]
    public async Task StatusChanges_ArePlatformOnly()
    {
        var tenant = _env.AddTenant();
        _env.Actor.IsPlatformAdmin = false;

        ResultAssert.Fails(
            await Svc.SuspendAsync(tenant.Id, new ChangeTenantStatusRequest()), TenantErrorKind.Forbidden);

        _env.Actor.IsPlatformAdmin = true;
        using var _ = _env.CurrentTenant.Change(tenant.Id);

        ResultAssert.Fails(
            await Svc.SuspendAsync(tenant.Id, new ChangeTenantStatusRequest()), TenantErrorKind.Forbidden);
        Assert.Equal(TenantStatus.Active, _env.Read(db => db.Tenants.Single(t => t.Id == tenant.Id)).Status);
    }

    [Fact]
    public async Task StatusChanges_TakeEffectImmediatelyForRequestHandling()
    {
        var tenant = _env.AddTenant();
        Assert.Equal(TenantStatus.Active, (await _env.InfoProvider.GetByIdAsync(tenant.Id))!.Status);

        await Svc.SuspendAsync(tenant.Id, new ChangeTenantStatusRequest());

        Assert.Equal(TenantStatus.Suspended, (await _env.InfoProvider.GetByIdAsync(tenant.Id))!.Status);
    }

    [Fact]
    public async Task Delete_RemovesAnArchivedTenant_WithItsSettingsAndMembers_ButKeepsTheAuditTrail()
    {
        var tenant = _env.AddTenant(status: TenantStatus.Archived);
        _env.AddSetting(tenant.Id, "general.timezone", "UTC");
        _env.AddMember(tenant.Id, Guid.NewGuid(), isOwner: true);
        _env.Seed(db => db.AuditEntries.Add(new TenantAuditEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Action = "earlier",
            OccurredAt = _env.UtcNow
        }));

        ResultAssert.Ok(await Svc.DeleteAsync(tenant.Id));

        Assert.Empty(_env.Read(db => db.Tenants.ToList()));
        Assert.Empty(_env.Read(db => db.Settings.ToList()));
        Assert.Empty(_env.Read(db => db.Members.ToList()));

        var actions = _env.Read(db => db.AuditEntries.Where(a => a.TenantId == tenant.Id).Select(a => a.Action).ToList());
        Assert.Contains("earlier", actions);
        Assert.Contains(TenantAuditActions.Deleted, actions);

        var deleted = Assert.Single(_env.EventsOf<TenantDeletedEvent>());
        Assert.Equal(tenant.Slug, deleted.Slug);
    }

    [Theory]
    [InlineData(TenantStatus.Pending)]
    [InlineData(TenantStatus.Active)]
    [InlineData(TenantStatus.Suspended)]
    public async Task Delete_RefusesATenantThatIsNotArchived(TenantStatus status)
    {
        var tenant = _env.AddTenant(status: status);

        ResultAssert.Fails(await Svc.DeleteAsync(tenant.Id), TenantErrorKind.Conflict);
        Assert.Single(_env.Read(db => db.Tenants.ToList()));
    }

    [Fact]
    public async Task Delete_UnknownTenantIsNotFound()
        => ResultAssert.Fails(await Svc.DeleteAsync(Guid.NewGuid()), TenantErrorKind.NotFound);

    [Fact]
    public async Task Delete_IsPlatformOnly()
    {
        var tenant = _env.AddTenant(status: TenantStatus.Archived);
        using var _ = _env.CurrentTenant.Change(tenant.Id);

        ResultAssert.Fails(await Svc.DeleteAsync(tenant.Id), TenantErrorKind.Forbidden);
        Assert.Single(_env.Read(db => db.Tenants.ToList()));
    }

    [Fact]
    public async Task Delete_ForgetsTheCachedTenant()
    {
        var tenant = _env.AddTenant(status: TenantStatus.Archived);
        await _env.InfoProvider.GetByIdAsync(tenant.Id);

        await Svc.DeleteAsync(tenant.Id);

        Assert.Null(await _env.InfoProvider.GetByIdAsync(tenant.Id));
    }
}

public sealed class TenantServiceReadTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private TenantService Svc => _env.CreateTenantService();

    [Fact]
    public async Task Get_ReturnsTheTenant()
    {
        var tenant = _env.AddTenant("Acme", "acme");

        var dto = ResultAssert.Ok(await Svc.GetAsync(tenant.Id));

        Assert.Equal("acme", dto.Slug);
    }

    [Fact]
    public async Task Get_UnknownTenantIsNotFound()
        => ResultAssert.Fails(await Svc.GetAsync(Guid.NewGuid()), TenantErrorKind.NotFound);

    [Fact]
    public async Task Get_BoundCallerSeesOnlyItsOwnTenant()
    {
        var own = _env.AddTenant("Own", "own");
        var other = _env.AddTenant("Other", "other");
        using var _ = _env.CurrentTenant.Change(own.Id);

        ResultAssert.Ok(await Svc.GetAsync(own.Id));
        ResultAssert.Fails(await Svc.GetAsync(other.Id), TenantErrorKind.Forbidden);

        // A made-up id gets the same answer, so a caller cannot probe which tenants exist.
        ResultAssert.Fails(await Svc.GetAsync(Guid.NewGuid()), TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task Get_WithoutAnyAccessIsRefused()
    {
        var tenant = _env.AddTenant();
        _env.Actor.IsPlatformAdmin = false;

        ResultAssert.Fails(await Svc.GetAsync(tenant.Id), TenantErrorKind.Forbidden);
    }

    [Fact]
    public async Task GetCurrent_NeedsAResolvedTenant()
        => ResultAssert.Fails(await Svc.GetCurrentAsync(), TenantErrorKind.Validation, "tenant.required");

    [Fact]
    public async Task GetCurrent_ReturnsTheRequestsTenant()
    {
        var tenant = _env.AddTenant("Acme", "acme");
        using var _ = _env.CurrentTenant.Change(tenant.Id);

        var dto = ResultAssert.Ok(await Svc.GetCurrentAsync());

        Assert.Equal(tenant.Id, dto.Id);
    }

    [Fact]
    public async Task List_IsOrderedBySlug_AndPaged()
    {
        _env.AddTenant("Bravo", "bravo");
        _env.AddTenant("Alpha", "alpha");
        _env.AddTenant("Charlie", "charlie");

        var page = ResultAssert.Ok(await Svc.ListAsync(new TenantListQuery { Page = 2, PageSize = 2 }));

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(new[] { "charlie" }, page.Items.Select(t => t.Slug));
    }

    [Fact]
    public async Task List_SearchesNameAndSlug_IgnoringCase()
    {
        _env.AddTenant("Green Grocers", "greens");
        _env.AddTenant("Blue Books", "bluebooks");

        var byName = ResultAssert.Ok(await Svc.ListAsync(new TenantListQuery { Search = "GROCER" }));
        var bySlug = ResultAssert.Ok(await Svc.ListAsync(new TenantListQuery { Search = "bluebo" }));

        Assert.Equal(new[] { "greens" }, byName.Items.Select(t => t.Slug));
        Assert.Equal(new[] { "bluebooks" }, bySlug.Items.Select(t => t.Slug));
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        _env.AddTenant("Live", "live", TenantStatus.Active);
        _env.AddTenant("Paused", "paused", TenantStatus.Suspended);

        var page = ResultAssert.Ok(await Svc.ListAsync(new TenantListQuery { Status = TenantStatus.Suspended }));

        Assert.Equal(new[] { "paused" }, page.Items.Select(t => t.Slug));
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 1000, 1, 100)]
    public async Task List_ClampsPaging(int page, int size, int expectedPage, int expectedSize)
    {
        var result = ResultAssert.Ok(await Svc.ListAsync(new TenantListQuery { Page = page, PageSize = size }));

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedSize, result.PageSize);
    }

    [Fact]
    public async Task List_IsPlatformOnly()
    {
        using var _ = _env.CurrentTenant.Change(Guid.NewGuid());

        ResultAssert.Fails(await Svc.ListAsync(new TenantListQuery()), TenantErrorKind.Forbidden);
    }
}