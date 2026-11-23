namespace CoreKit.Access.Abstractions;

/// <summary>A person's standing in a scope. Owners hold every permission.</summary>
public sealed record AccessMember(bool IsOwner, long MembershipGeneration = 1);

/// <summary>
/// The place roles apply to: a type and an id.
/// </summary>
public readonly record struct AccessScope(string Type, Guid Id)
{
    public const string TenantType = "tenant";
    public const string PlatformType = "platform";
    public const int MaxTypeLength = 30;

    public static readonly Guid PlatformId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static AccessScope ForTenant(Guid tenantId) => new(TenantType, tenantId);

    public static AccessScope ForPlatform(Guid platformId) => new(PlatformType, platformId);

    public bool IsValid => !string.IsNullOrWhiteSpace(Type) && Type.Length <= MaxTypeLength && Id != Guid.Empty;

    public override string ToString() => $"{Type}:{Id:N}";
}

/// <summary>
/// Who belongs to a scope. Returns null when the user does not belong to the scope.
/// </summary>
public interface IAccessMembership
{
    Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default);
}

/// <summary>
/// The one question every module asks before doing something sensitive: may the signed-in user do this
/// here? Fail closed: no permission, no tenant, or no user all mean false.
/// </summary>
public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default);

    /// <summary>
    /// Fresh permission check that bypasses the effective-access cache. Use for write operations
    /// (role/assignment/platform membership management) so a permission just revoked is not still
    /// honored through a remembered copy.
    /// </summary>
    Task<bool> HasFreshPermissionAsync(string permission, CancellationToken ct = default);

    /// <summary>
    /// Always answered against the fixed platform scope rather than the ambient request scope.
    /// Fail closed: no user, or no permission, means false.
    /// </summary>
    Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default);
}

/// <summary>
/// A membership adapter registered alongside the composite router.
/// </summary>
public interface IAccessMembershipAdapter : IAccessMembership
{
    Task<long> GetLastEventSequenceAsync(Guid tenantId, CancellationToken ct = default);
    Task<long> GetLastAppliedSequenceAsync(Guid tenantId, CancellationToken ct = default);
}