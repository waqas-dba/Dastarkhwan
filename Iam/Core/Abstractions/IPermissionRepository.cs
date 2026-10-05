using Iam.Core.Entities;

namespace Iam.Core.Abstractions;

public interface IPermissionRepository
{
    Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct = default);

    void Add(Permission permission);
}