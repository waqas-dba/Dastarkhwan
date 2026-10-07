using CoreKit.IAM.Entities;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.IAM.Repositories;

internal sealed class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly IamDbContext _db;

    public EmailVerificationTokenRepository(IamDbContext db) => _db = db;

    public Task<EmailVerificationToken?> FindByHashWithUserAsync(string tokenHash, CancellationToken ct = default)
        => _db.EmailVerificationTokens
            .AsNoTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task<bool> HasCreatedSinceAsync(Guid userId, DateTime cutoff, CancellationToken ct = default)
        => _db.EmailVerificationTokens.AnyAsync(t => t.UserId == userId && t.CreatedAt > cutoff, ct);

    public async Task ReplaceOpenTokensAsync(EmailVerificationToken newToken, DateTime now, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Only the newest link works: earlier unused ones are closed.
        await _db.EmailVerificationTokens
            .Where(t => t.UserId == newToken.UserId && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, (DateTime?)now), ct);

        _db.EmailVerificationTokens.Add(newToken);
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }

    public async Task<bool> ConfirmEmailAsync(Guid tokenId, Guid userId, DateTime now, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Atomic: if the link is opened twice at the same moment, only one request gets the token.
        var claimed = await _db.EmailVerificationTokens
            .Where(t => t.Id == tokenId && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, (DateTime?)now), ct);

        if (claimed == 0)
            return false;

        await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.EmailConfirmed, true)
                .SetProperty(u => u.UpdatedAt, (DateTime?)now), ct);

        await transaction.CommitAsync(ct);

        return true;
    }
}