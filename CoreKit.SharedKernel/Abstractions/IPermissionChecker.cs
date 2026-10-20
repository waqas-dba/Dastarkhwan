namespace CoreKit.SharedKernel;

/// <summary>
/// The one question every module asks before doing something sensitive: may the signed-in user do this
/// here? "Here" is the current scope, normally the current tenant. The Access module provides it, so other
/// modules never reference Access. No permission, no tenant, or no user all mean false.
/// </summary>
public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default);
}