using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Constants;

namespace CoreKit.Tenant.Abstractions;

/// <summary>
/// Who is acting. The IAM integration provides this from the signed-in user.
/// </summary>
public sealed class TenantActor : ITenantActor
{
    private readonly ITenantCurrentUser? _currentUser;
    private readonly ITenantPermissionChecker? _permissions;

    public TenantActor(ITenantCurrentUser? currentUser = null, ITenantPermissionChecker? permissions = null)
    {
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public Guid? UserId => _currentUser?.UserId;

    public Task<bool> IsPlatformAdminAsync(CancellationToken ct = default)
        => _permissions is null
            ? Task.FromResult(false)
            : _permissions.HasPlatformPermissionAsync(TenantPermissionNames.Platform, ct);
}