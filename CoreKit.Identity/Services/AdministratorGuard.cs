using System.Collections.Generic;
using CoreKit.Identity.Interfaces;

namespace CoreKit.Identity.Services;

/// <summary>
/// Prevents the one mistake that locks everybody out: removing, deactivating or deleting the last
/// administrator. The host configures which user IDs count as administrators; Access owns the
/// authoritative role data, so this guard is a best-effort check based on the configured list.
/// </summary>
public sealed class AdministratorGuard : IAdministratorGuard
{
    private readonly IReadOnlyCollection<Guid> _administratorUserIds;

    public AdministratorGuard(IEnumerable<Guid> administratorUserIds)
    {
        _administratorUserIds = administratorUserIds is IReadOnlyCollection<Guid> collection
            ? collection
            : new List<Guid>(administratorUserIds);
    }

    public Task<bool> IsLastActiveAdministratorAsync(Guid userId, CancellationToken ct = default)
    {
        return Task.FromResult(
            _administratorUserIds.Count == 1 && _administratorUserIds.Contains(userId));
    }
}
