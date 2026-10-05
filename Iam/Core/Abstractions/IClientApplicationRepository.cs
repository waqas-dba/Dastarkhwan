using Iam.Core.Entities;

namespace Iam.Core.Abstractions;

public interface IClientApplicationRepository
{
    Task<ClientApplication?> GetByClientIdAsync(string clientId, CancellationToken ct = default);
    Task<ClientApplication?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ClientApplication>> GetAllAsync(CancellationToken ct = default);

    void Add(ClientApplication client);
}