namespace CoreKit.Access.Abstractions;

/// <summary>
/// The one question every module asks before doing something sensitive: may the signed-in user do this
/// here? "Here" is the current scope, normally the current tenant. Access provides it, so other modules
/// never reference Access. No permission, no tenant, or no user all mean false.
/// </summary>
public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default);

    /// <summary>
    /// The same question, always answered against the fixed platform scope rather than the ambient
    /// request scope. Used by platform-level operations (for example Identity user management) that
    /// are not inside a tenant. Fail closed: no user, or no permission, means false.
    /// </summary>
    Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default);
}
