using CoreKit.IAM.Entities;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.IAM.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IamDbContext _db;

    public RefreshTokenRepository(IamDbContext db) => _db = db;

    public Task<RefreshToken?> FindByHashWithUserAsync(string tokenHash, CancellationToken ct = default)
        => _db.RefreshTokens
            .AsNoTracking()
            .Include(t => t.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryRevokeAsync(Guid tokenId, DateTime now, CancellationToken ct = default)
    {
        var changed = await _db.RefreshTokens
            .Where(t => t.Id == tokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now), ct);

        return changed > 0;
    }

    public async Task RevokeByHashAsync(string tokenHash, DateTime now, CancellationToken ct = default)
    {
        await _db.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now), ct);
    }

    public Task<int> RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken ct = default)
        => _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now), ct);
}