using Iam.Core.Abstractions;
using Iam.Core.Entities;
using Iam.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Iam.Data.Repositories;

public sealed class SessionRepository : ISessionRepository
{
    private readonly IamDbContext _db;

    public SessionRepository(IamDbContext db) => _db = db;

    public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Session>> ListActiveByUserAsync(
        Guid userId, DateTimeOffset now, CancellationToken ct = default)
        => await _db.Sessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now)
            .OrderBy(s => s.LastUsedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Session>> ListActiveByUserAndClientAsync(
        Guid userId, Guid clientApplicationId, DateTimeOffset now, CancellationToken ct = default)
        => await _db.Sessions
            .Where(s => s.UserId == userId
                        && s.ClientApplicationId == clientApplicationId
                        && s.RevokedAtUtc == null
                        && s.ExpiresAtUtc > now)
            .OrderBy(s => s.LastUsedAtUtc)
            .ToListAsync(ct);

    public void Add(Session session) => _db.Sessions.Add(session);
}