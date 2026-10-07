namespace CoreKit.IAM.Interfaces;

/// <summary>
/// Prevents the one mistake that locks everybody out: removing, deactivating or deleting
/// the last administrator.
/// </summary>
public interface IAdministratorGuard
{
    Task<bool> IsLastActiveAdministratorAsync(Guid userId, CancellationToken ct = default);
}