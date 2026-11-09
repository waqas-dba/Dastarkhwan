using CoreKit.SharedKernel.Abstractions;
using CoreKit.Tenant.Constants;

namespace CoreKit.Tenant.Abstractions;

/// <summary>Who is acting. Resolved from the signed-in user provided by the shared kernel.</summary>
public sealed class TenantActor : ITenantActor
{
    private readonly ICurrentUser? _currentUser;

    public TenantActor(ICurrentUser? currentUser = null) => _currentUser = currentUser;

    public Guid? UserId => _currentUser?.UserId;

    public bool IsPlatformAdmin
        => _currentUser?.Permissions.Contains(TenantPermissionNames.Platform) == true;
}
