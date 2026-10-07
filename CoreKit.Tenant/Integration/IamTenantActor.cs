using CoreKit.IAM.Services;

namespace CoreKit.Tenant.Integration;

/// <summary>Takes the acting user from IAM. Platform administrators hold the tenants.platform permission.</summary>
internal sealed class IamTenantActor : ITenantActor
{
    private readonly ICurrentUserService? _currentUser;

    public IamTenantActor(ICurrentUserService? currentUser = null) => _currentUser = currentUser;

    public Guid? UserId => _currentUser?.UserId;

    public bool IsPlatformAdmin
        => _currentUser?.Permissions.Contains(TenantPermissionNames.Platform) == true;
}