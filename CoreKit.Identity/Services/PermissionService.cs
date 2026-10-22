using CoreKit.Identity.Common;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;

namespace CoreKit.Identity.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _permissionRepository;

    public PermissionService(IPermissionRepository permissionRepository)
        => _permissionRepository = permissionRepository;

    public async Task<IdentityResult<IReadOnlyList<PermissionDto>>> ListAsync(CancellationToken ct = default)
        => Common.IdentityResult.Success(await _permissionRepository.ListAsync(ct));

    public async Task<IdentityResult<PermissionDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var permission = await _permissionRepository.FindAsync(id, ct);

        return permission is null ? IdentityErrors.NotFound("Permission") : Common.IdentityResult.Success(permission);
    }
}