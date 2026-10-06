using CoreKit.IAM.Models;

namespace CoreKit.IAM.Interfaces;

/// <summary>
/// Read-only on purpose. Permissions are created by the seeder or by each module's own setup,
/// so no one can invent a permission through the API in production.
/// </summary>
public interface IPermissionService
{
    Task<IamResult<IReadOnlyList<PermissionDto>>> ListAsync(CancellationToken ct = default);

    Task<IamResult<PermissionDto>> GetAsync(Guid id, CancellationToken ct = default);
}