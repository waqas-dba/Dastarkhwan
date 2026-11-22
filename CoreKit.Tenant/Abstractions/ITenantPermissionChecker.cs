namespace CoreKit.Tenant.Abstractions;

/// <summary>
/// Checks whether the current user has a permission. Access is the authority for permissions,
/// so the implementation is provided by Access through Integration.
/// </summary>
public interface ITenantPermissionChecker
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default);

    /// <summary>
    /// Fresh permission check that bypasses the effective-access cache. Use for write operations
    /// (tenant membership management) so a permission just revoked is not still honored through
    /// a remembered copy.
    /// </summary>
    Task<bool> HasFreshPermissionAsync(string permission, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the current user has a platform-level permission. Always answered against
    /// the fixed platform scope rather than the ambient request scope.
    /// Fail closed: no user, or no permission, means false.
    /// </summary>
    Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default);
}