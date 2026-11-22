namespace CoreKit.Identity.Interfaces;

public interface IUserLockoutRepository
{
    /// <summary>Resets the failed-login counter and removes any lockout. Returns false when the user no longer exists.</summary>
    Task<bool> ClearLockoutAsync(Guid userId, CancellationToken ct = default);
}
