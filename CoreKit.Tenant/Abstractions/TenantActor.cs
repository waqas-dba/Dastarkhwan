using CoreKit.Access.Abstractions;
using CoreKit.Tenant.Constants;

namespace CoreKit.Tenant.Abstractions;

/// <summary>
/// Who is acting. The user comes from the signed-in token; the platform flag is answered by Access,
/// which is the authority for permissions. Tokens carry no permission claims, so reading them here
/// would always answer false.
/// </summary>
public sealed class TenantActor : ITenantActor
{
    private readonly ICurrentUser? _currentUser;
    private readonly IPermissionChecker? _permissions;

    public TenantActor(ICurrentUser? currentUser = null, IPermissionChecker? permissions = null)
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