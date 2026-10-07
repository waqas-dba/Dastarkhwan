using CoreKit.IAM.Common;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;

namespace CoreKit.IAM.Services;

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