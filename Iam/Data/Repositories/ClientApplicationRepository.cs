using Iam.Core.Abstractions;
using Iam.Core.Entities;
using Iam.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Iam.Data.Repositories;

public sealed class ClientApplicationRepository : IClientApplicationRepository
{
    private readonly IamDbContext _db;

    public ClientApplicationRepository(IamDbContext db) => _db = db;

    public Task<ClientApplication?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        var normalized = clientId.Trim().ToLowerInvariant();
        return _db.ClientApplications.FirstOrDefaultAsync(c => c.ClientId == normalized, ct);
    }

    public Task<ClientApplication?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.ClientApplications.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ClientApplication>> GetAllAsync(CancellationToken ct = default)
        => await _db.ClientApplications
            .AsNoTracking()
            .OrderBy(c => c.ClientId)
            .ToListAsync(ct);

    public void Add(ClientApplication client) => _db.ClientApplications.Add(client);
}