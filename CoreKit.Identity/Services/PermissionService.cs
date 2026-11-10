using CoreKit.Identity.Common;
using CoreKit.Identity.Constants;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;

namespace CoreKit.Identity.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly ICurrentUserService _currentUser;

    public PermissionService(IPermissionRepository permissionRepository, ICurrentUserService currentUser)
    {
        _permissionRepository = permissionRepository;
        _currentUser = currentUser;
    }

    public async Task<IdentityResult<IReadOnlyList<PermissionDto>>> ListAsync(CancellationToken ct = default)
    {
        if (!_currentUser.Permissions.Contains(IdentityPermissionNames.PermissionsRead))
            return IdentityErrors.AccessDenied("You cannot view permissions.");

        return Common.IdentityResult.Success(await _permissionRepository.ListAsync(ct));
    }

    public async Task<IdentityResult<PermissionDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (!_currentUser.Permissions.Contains(IdentityPermissionNames.PermissionsRead))
            return IdentityErrors.AccessDenied("You cannot view permissions.");

        var permission = await _permissionRepository.FindAsync(id, ct);

        return permission is null ? IdentityErrors.NotFound("Permission") : Common.IdentityResult.Success(permission);
    }
}