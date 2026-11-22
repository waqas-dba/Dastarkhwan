namespace CoreKit.Tenant.Abstractions;

/// <summary>
/// Validates that a user is suitable to be a tenant owner (exists and is active).
/// Implemented by the Integration layer using Identity's user repository.
/// </summary>
public interface IOwnerUserValidator
{
    /// <summary>Returns true if the user exists and is active.</summary>
    Task<bool> ValidateOwnerUserAsync(Guid userId, CancellationToken ct = default);
}