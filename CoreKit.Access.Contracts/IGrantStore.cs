namespace CoreKit.Access.Contracts;

public interface IGrantStore
{
    Task<bool> TryConsumeAsync(Guid grantId, DateTime expiresAtUtc, CancellationToken ct = default);
}