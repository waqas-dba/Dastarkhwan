namespace CoreKit.Tenant.Tests;

public sealed class CurrentTenantTests
{
    private readonly CurrentTenant _current = new();

    [Fact]
    public void StartsUnresolved()
    {
        Assert.False(_current.IsResolved);
        Assert.Null(_current.Id);
        Assert.Throws<TenantNotResolvedException>(() => _current.RequiredId);
    }

    [Fact]
    public void Set_ResolvesTheTenant()
    {
        var id = Guid.NewGuid();

        _current.Set(id);

        Assert.True(_current.IsResolved);
        Assert.Equal(id, _current.Id);
        Assert.Equal(id, _current.RequiredId);
    }

    [Fact]
    public void Set_RejectsAnEmptyId()
        => Assert.Throws<ArgumentException>(() => _current.Set(Guid.Empty));

    [Fact]
    public void Change_SwitchesTemporarilyAndRestores()
    {
        var original = Guid.NewGuid();
        var other = Guid.NewGuid();
        _current.Set(original);

        using (_current.Change(other))
            Assert.Equal(other, _current.Id);

        Assert.Equal(original, _current.Id);
    }

    [Fact]
    public void Change_ToNullActsAsNoTenant()
    {
        _current.Set(Guid.NewGuid());

        using (_current.Change(null))
            Assert.False(_current.IsResolved);

        Assert.True(_current.IsResolved);
    }

    [Fact]
    public void Change_NestsInTheRightOrder()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        using (_current.Change(a))
        {
            using (_current.Change(b))
                Assert.Equal(b, _current.Id);

            Assert.Equal(a, _current.Id);
        }

        Assert.False(_current.IsResolved);
    }

    [Fact]
    public void Change_RejectsAnEmptyId()
        => Assert.Throws<ArgumentException>(() => _current.Change(Guid.Empty));

    [Fact]
    public void Dispose_TwiceDoesNotRestoreTwice()
    {
        var a = Guid.NewGuid();
        var scope = _current.Change(a);
        scope.Dispose();
        _current.Set(Guid.NewGuid());
        var now = _current.Id;

        scope.Dispose();

        Assert.Equal(now, _current.Id);
    }
}

public sealed class TenantPlatformAccessTests
{
    private readonly FakeTenantActor _actor = new();

    [Fact]
    public void NotGrantedByDefault()
        => Assert.False(new TenantPlatformAccess(_actor).IsGranted);

    [Fact]
    public void PlatformAdminsAreGranted()
    {
        _actor.IsPlatformAdmin = true;

        Assert.True(new TenantPlatformAccess(_actor).IsGranted);
    }

    [Fact]
    public void Grant_IsTemporary()
    {
        var access = new TenantPlatformAccess(_actor);

        using (access.Grant())
            Assert.True(access.IsGranted);

        Assert.False(access.IsGranted);
    }

    [Fact]
    public void Grant_NestsAndSurvivesDoubleDispose()
    {
        var access = new TenantPlatformAccess(_actor);
        var outer = access.Grant();
        var inner = access.Grant();

        inner.Dispose();
        inner.Dispose();
        Assert.True(access.IsGranted);

        outer.Dispose();
        Assert.False(access.IsGranted);
    }
}

public sealed class TenantAccessGuardTests
{
    private readonly FakeTenantActor _actor = new();
    private readonly CurrentTenant _current = new();
    private readonly TenantPlatformAccess _platform;
    private readonly TenantAccessGuard _guard;

    public TenantAccessGuardTests()
    {
        _platform = new TenantPlatformAccess(_actor);
        _guard = new TenantAccessGuard(_current, _platform, _actor);
    }

    [Fact]
    public void Platform_UnresolvedAdmin_MayDoPlatformWorkAndTouchAnyTenant()
    {
        _actor.IsPlatformAdmin = true;

        Assert.Null(_guard.EnsurePlatformScope());
        Assert.Null(_guard.EnsureCanAccess(Guid.NewGuid()));
    }

    [Fact]
    public void Unresolved_WithoutPlatformAccess_IsRefusedEverywhere()
    {
        Assert.Equal(TenantErrors.PlatformOnly, _guard.EnsurePlatformScope());
        Assert.Equal(TenantErrors.PlatformOnly, _guard.EnsureCanAccess(Guid.NewGuid()));
    }

    [Fact]
    public void Bound_MayTouchOnlyItsOwnTenant()
    {
        var own = Guid.NewGuid();
        _current.Set(own);

        Assert.Null(_guard.EnsureCanAccess(own));
        Assert.Equal(TenantErrors.CrossTenantAccess, _guard.EnsureCanAccess(Guid.NewGuid()));
    }

    [Fact]
    public void Bound_IsNeverPlatformScope_EvenForAdmins()
    {
        _actor.IsPlatformAdmin = true;
        _current.Set(Guid.NewGuid());

        Assert.Equal(TenantErrors.PlatformOnly, _guard.EnsurePlatformScope());
        Assert.Equal(TenantErrors.CrossTenantAccess, _guard.EnsureCanAccess(Guid.NewGuid()));
    }

    [Fact]
    public void GrantedTrustedCode_ActsAsPlatform()
    {
        using (_platform.Grant())
        {
            Assert.Null(_guard.EnsurePlatformScope());
            Assert.Null(_guard.EnsureCanAccess(Guid.NewGuid()));
        }
    }

    [Fact]
    public void SelfOrPlatform_AllowsTheUserThemselves()
    {
        var userId = Guid.NewGuid();
        _actor.UserId = userId;

        Assert.Null(_guard.EnsureSelfOrPlatform(userId));
        Assert.Equal(TenantErrors.CrossTenantAccess, _guard.EnsureSelfOrPlatform(Guid.NewGuid()));
    }

    [Fact]
    public void SelfOrPlatform_AllowsPlatformAdmins()
    {
        _actor.IsPlatformAdmin = true;

        Assert.Null(_guard.EnsureSelfOrPlatform(Guid.NewGuid()));
    }

    [Fact]
    public void SelfOrPlatform_RefusesAnonymousCallers()
        => Assert.NotNull(_guard.EnsureSelfOrPlatform(Guid.NewGuid()));

    [Fact]
    public void Exposes_TheCurrentTenant()
    {
        Assert.False(_guard.IsTenantBound);
        Assert.Null(_guard.CurrentTenantId);

        var id = Guid.NewGuid();
        _current.Set(id);

        Assert.True(_guard.IsTenantBound);
        Assert.Equal(id, _guard.CurrentTenantId);
    }
}