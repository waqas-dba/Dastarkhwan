using Iam.Core.Entities;

namespace Iam.Core.Abstractions;

public interface ISessionRepository
{
    /// <summary>Loads a tracked session, so it can be rotated or revoked.</summary>
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Active sessions of a user across all apps, oldest use first.</summary>
    Task<IReadOnlyList<Session>> ListActiveByUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Active sessions of a user in one app, oldest use first. Used to enforce the device limit.</summary>
    Task<IReadOnlyList<Session>> ListActiveByUserAndClientAsync(
        Guid userId, Guid clientApplicationId, DateTimeOffset now, CancellationToken ct = default);

    void Add(Session session);
}