using CoreKit.Identity.Models;

namespace CoreKit.Identity.Interfaces;

/// <summary>
/// Read-only on purpose. Permissions are created by the seeder or by each module's own setup,
/// so no one can invent a permission through the API in production.
/// </summary>
public interface IPermissionService
{
    Task<IdentityResult<IReadOnlyList<PermissionDto>>> ListAsync(CancellationToken ct = default);

    Task<IdentityResult<PermissionDto>> GetAsync(Guid id, CancellationToken ct = default);
}