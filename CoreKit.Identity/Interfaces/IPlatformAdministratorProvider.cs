namespace CoreKit.Identity.Interfaces;

/// <summary>
/// Provides the set of user IDs that hold the platform Administrator role.
/// Implemented by Access module; consumed by Identity for last-administrator protection.
/// </summary>
public interface IPlatformAdministratorProvider
{
    Task<IReadOnlyList<Guid>> GetPlatformAdministratorUserIdsAsync(CancellationToken ct = default);
}