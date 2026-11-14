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

    /// <summary>
    /// Makes the user an active platform member (idempotent; reactivates with a new generation if they
    /// were revoked). Trusted system provisioning — no permission check. Never expose to callers.
    /// </summary>
    Task AddPlatformMemberAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Revokes the user's platform membership (idempotent). Trusted system provisioning.</summary>
    Task RemovePlatformMemberAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the user's platform-scope role assignments with exactly these roles. Trusted system
    /// provisioning: the caller has already authorized the change. Throws when the user is not an
    /// active platform member or a role id is not a platform-scope role.
    /// </summary>
    Task SetPlatformUserRolesAsync(
        Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default);

    /// <summary>
    /// Finds or creates a platform-scope role with this name and sets its permissions to exactly the
    /// given set (adds missing, removes extra). Trusted system provisioning, used by seeding to keep the
    /// Administrator role in step with the catalog. Returns the role id.
    /// </summary>
    Task<Guid> EnsurePlatformRoleAsync(
        string name, string? description, IReadOnlyCollection<string> permissions, CancellationToken ct = default);
}