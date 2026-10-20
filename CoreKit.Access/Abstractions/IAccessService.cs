namespace CoreKit.Access.Abstractions;

/// <summary>What a person may do in a scope, resolved from membership and roles.</summary>
public sealed record EffectiveAccess(
    bool IsMember, bool IsOwner, IReadOnlySet<string> Permissions, IReadOnlyList<string> Roles)
{
    public static EffectiveAccess None { get; } =
        new(false, false, new HashSet<string>(StringComparer.Ordinal), Array.Empty<string>());

    public bool Has(string permission) => IsMember && (IsOwner || Permissions.Contains(permission));
}

public interface IAccessService
{
    Task<EffectiveAccess> GetAccessAsync(Guid userId, AccessScope scope, CancellationToken ct = default);

    Task<bool> HasPermissionAsync(Guid userId, AccessScope scope, string permission, CancellationToken ct = default);

    /// <summary>For the signed-in user in the current scope. False when either is missing.</summary>
    Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default);

    /// <summary>For the signed-in user in the current scope.</summary>
    Task<AccessResult<EffectiveAccess>> GetMyAccessAsync(CancellationToken ct = default);
}