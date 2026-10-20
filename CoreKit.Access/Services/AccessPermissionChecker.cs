namespace CoreKit.Access.Services;

/// <summary>Lets any module ask "may the signed-in user do this here?" through the shared kernel.</summary>
internal sealed class AccessPermissionChecker : IPermissionChecker
{
    private readonly IAccessService _access;

    public AccessPermissionChecker(IAccessService access) => _access = access;

    public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
        => _access.HasPermissionAsync(permission, ct);
}