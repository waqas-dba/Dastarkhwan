using CoreKit.Access.Contracts;
using CoreKit.Identity.Constants;
using CoreKit.Identity.Contracts;

namespace Dastarkhwan.Integration.Identity;

public sealed class PermissionBasedIdentityAdminAuthorizer : IIdentityAdminAuthorizer
{
    private readonly IPermissionChecker _permissionChecker;
    public PermissionBasedIdentityAdminAuthorizer(IPermissionChecker permissionChecker)
        => _permissionChecker = permissionChecker;

    public async Task<bool> IsAllowedAsync(IdentityAdminOperation operation, CancellationToken ct = default)
    {
        var permission = operation switch
        {
            IdentityAdminOperation.ReadUsers   => IdentityAccessPermissionNames.UsersRead,
            IdentityAdminOperation.CreateUsers => IdentityAccessPermissionNames.UsersCreate,
            IdentityAdminOperation.UpdateUsers => IdentityAccessPermissionNames.UsersUpdate,
            IdentityAdminOperation.DeleteUsers => IdentityAccessPermissionNames.UsersDelete,
            _ => null
        };
        return permission is not null
            && await _permissionChecker.HasPlatformPermissionAsync(permission, ct);
    }
}