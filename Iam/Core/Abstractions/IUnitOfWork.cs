namespace Iam.Core.Abstractions;

/// <summary>
/// Saves everything that was changed during one request as a single all-or-nothing unit.
/// Repositories only stage changes; nothing reaches the database until SaveChangesAsync.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}