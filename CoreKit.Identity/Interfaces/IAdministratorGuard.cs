namespace CoreKit.Identity.Interfaces;

/// <summary>
/// Prevents the one mistake that locks everybody out: removing, deactivating or deleting
/// the last administrator.
/// </summary>
public interface IAdministratorGuard
{
    /// <summary>
    /// Checks if the user is the last active administrator. Uses a PostgreSQL advisory lock
    /// to serialize concurrent checks. The lock is released when the caller's transaction commits
    /// or rolls back. Call this within a transaction that also performs the protected action
    /// (deactivate/delete) to ensure atomicity.
    /// </summary>
    Task<bool> IsLastActiveAdministratorAsync(Guid userId, CancellationToken ct = default);
}