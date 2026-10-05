using Iam.Core.Entities;

namespace Iam.Core.Abstractions;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<bool> IsAssignedToAnyUserAsync(Guid roleId, CancellationToken ct = default);

    void Add(Role role);
    void Remove(Role role);
}