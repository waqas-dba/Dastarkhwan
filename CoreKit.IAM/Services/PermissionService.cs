using CoreKit.Identity.Common;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;

namespace CoreKit.Identity.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _permissionRepository;

    public PermissionService(IPermissionRepository permissionRepository)
        => _permissionRepository = permissionRepository;

    public async Task<IamResult<IReadOnlyList<PermissionDto>>> ListAsync(CancellationToken ct = default)
        => IamResult.Success(await _permissionRepository.ListAsync(ct));

    public async Task<IamResult<PermissionDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var permission = await _permissionRepository.FindAsync(id, ct);

        return permission is null ? IamErrors.NotFound("Permission") : IamResult.Success(permission);
    }
}