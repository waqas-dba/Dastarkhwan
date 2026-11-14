using CoreKit.Identity.Entities;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.Identity.Repositories;

public sealed class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly IdentityDbContext _db;

    public EmailVerificationTokenRepository(IdentityDbContext db) => _db = db;

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

    public async Task<bool> ConfirmEmailAsync(
        Guid tokenId, Guid userId, string tokenNormalizedEmail, DateTime now, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Atomic: if the link is opened twice at the same moment, only one request gets the token.
        var claimed = await _db.EmailVerificationTokens
            .Where(t => t.Id == tokenId && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, (DateTime?)now), ct);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        // Bind the confirmation to the address the token was issued for, evaluated against the
        // current row inside the transaction. If the user's normalized email no longer matches
        // (a stale token for a different address), roll back the token claim as well, so the
        // link is not consumed by a failed confirmation. EmailConfirmed is only ever set from
        // false to true: no SecurityVersion change, no session revocation.
        var confirmed = await _db.Users
            .Where(u => u.Id == userId && u.NormalizedEmail == tokenNormalizedEmail)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.EmailConfirmed, true)
                .SetProperty(u => u.UpdatedAt, (DateTime?)now), ct);

        if (confirmed == 0)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        await transaction.CommitAsync(ct);

        return true;
    }
}