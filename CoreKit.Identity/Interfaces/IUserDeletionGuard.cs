namespace CoreKit.Identity.Interfaces;

/// <summary>
/// Validates whether a user can be deleted. Used for cross-module deletion checks.
/// Implemented by the Integration layer.
/// </summary>
public interface IUserDeletionGuard
{
    /// <summary>Returns true if the user can be deleted (not a tenant owner, etc.).</summary>
    Task<bool> CanDeleteUserAsync(Guid userId, CancellationToken ct = default);
}