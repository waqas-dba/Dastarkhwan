namespace CoreKit.Tenant.Services;

/// <summary>
/// The tenant boundary. A caller bound to a tenant can only touch that tenant. A caller bound to none must
/// have platform access. Anything else is refused, so a missing tenant claim never turns into extra power.
/// Each method returns an error, or null when the call is allowed.
/// </summary>
public sealed class TenantAccessGuard
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantPlatformAccess _platform;
    private readonly ITenantActor _actor;

    public TenantAccessGuard(ICurrentTenant currentTenant, ITenantPlatformAccess platform, ITenantActor actor)
    {
        _currentTenant = currentTenant;
        _platform = platform;
        _actor = actor;
    }

    public bool IsTenantBound => _currentTenant.IsResolved;

    public Guid? CurrentTenantId => _currentTenant.Id;

    /// <summary>For work that spans tenants: listing, creating, changing status, deleting.</summary>
    public TenantError? EnsurePlatformScope()
        => _currentTenant.IsResolved || !_platform.IsGranted ? TenantErrors.PlatformOnly : null;

    /// <summary>For work on one tenant.</summary>
    public TenantError? EnsureCanAccess(Guid tenantId)
    {
        if (_currentTenant.IsResolved)
            return _currentTenant.Id == tenantId ? null : TenantErrors.CrossTenantAccess;

        return _platform.IsGranted ? null : TenantErrors.PlatformOnly;
    }

    /// <summary>For data about one user: the user themselves, or platform access.</summary>
    public TenantError? EnsureSelfOrPlatform(Guid userId)
        => _platform.IsGranted || _actor.UserId == userId ? null : TenantErrors.CrossTenantAccess;
}