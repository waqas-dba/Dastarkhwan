namespace CoreKit.Access.Abstractions;

/// <summary>
/// Housekeeping for trusted code such as a bridge reacting to events. It has no permission checks:
/// never expose it directly to callers.
/// </summary>
public interface IAccessMaintenance
{
    /// <summary>Removes every role the user holds in the scope (for example when they leave).</summary>
    Task RemoveUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default);

    /// <summary>Deletes all roles and assignments of a scope (for example when a tenant is deleted).</summary>
    Task RemoveScopeAsync(AccessScope scope, CancellationToken ct = default);

    /// <summary>Copies the role templates into a new scope. Does nothing if the scope already has roles.</summary>
    Task ProvisionScopeAsync(AccessScope scope, CancellationToken ct = default);
}