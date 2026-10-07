using CoreKit.IAM.Models;

namespace CoreKit.IAM.Interfaces;

public interface IPermissionRepository
{
    Task<IReadOnlyList<PermissionDto>> ListAsync(CancellationToken ct = default);

    Task<PermissionDto?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>True when every id exists (an empty list is true).</summary>
    Task<bool> AllExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Names of the active permissions granted by the given roles.</summary>
    Task<IReadOnlyList<string>> GetActiveNamesForRolesAsync(
        IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default);
}