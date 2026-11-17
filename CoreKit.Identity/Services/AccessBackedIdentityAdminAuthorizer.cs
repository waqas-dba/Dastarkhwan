using CoreKit.Access.Abstractions;
using CoreKit.Identity.Constants;
using CoreKit.Identity.Interfaces;

namespace CoreKit.Identity.Services;

/// <summary>
/// Interim adapter: answers <see cref="IIdentityAdminAuthorizer"/> by asking Access for the matching
/// platform permission, exactly as Identity did before the port existed. It is removed when Identity
/// drops its Access reference. Every unrecognised operation is denied.
/// </summary>
public sealed class AccessBackedIdentityAdminAuthorizer : IIdentityAdminAuthorizer
{
    private readonly IPermissionChecker _permissions;

    public AccessBackedIdentityAdminAuthorizer(IPermissionChecker permissions) => _permissions = permissions;

    public Task<bool> IsAllowedAsync(IdentityAdminOperation operation, CancellationToken ct = default)
    {
        var permission = operation switch
        {
            IdentityAdminOperation.ReadUsers => IdentityAccessPermissionNames.UsersRead,
            IdentityAdminOperation.CreateUsers => IdentityAccessPermissionNames.UsersCreate,
            IdentityAdminOperation.UpdateUsers => IdentityAccessPermissionNames.UsersUpdate,
            IdentityAdminOperation.DeleteUsers => IdentityAccessPermissionNames.UsersDelete,
            _ => null
        };

        return permission is null
            ? Task.FromResult(false)
            : _permissions.HasPlatformPermissionAsync(permission, ct);
    }
}