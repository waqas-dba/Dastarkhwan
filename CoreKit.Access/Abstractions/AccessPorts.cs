namespace CoreKit.Access.Abstractions;

/// <summary>
/// Where the current request happens. By default it is the current tenant (from the shared kernel's
/// ICurrentTenant), so roles belong to a tenant. Another module can supply its own, for example a store.
/// </summary>
public interface IAccessScopeContext
{
    AccessScope? Current { get; }
}

/// <summary>A person's standing in a scope. Owners hold every permission.</summary>
public sealed record AccessMember(bool IsOwner);

/// <summary>
/// Who belongs to a scope. By default tenant membership (from the shared kernel's ITenantMembershipReader).
/// Without a tenant module, nobody is a member, so nobody is granted anything.
/// </summary>
public interface IAccessMembership
{
    /// <summary>Null when the user does not belong to the scope.</summary>
    Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default);
}