namespace CoreKit.Access.Abstractions;

/// <summary>Where the current request happens. A bridge supplies it (for example from the current tenant).</summary>
public interface IAccessScopeContext
{
    AccessScope? Current { get; }
}

/// <summary>Who is acting. By default it is read from the "sub" claim of the signed-in user.</summary>
public interface IAccessActor
{
    Guid? UserId { get; }
}

/// <summary>A person's standing in a scope. Owners hold every permission.</summary>
public sealed record AccessMember(bool IsOwner);

/// <summary>
/// Who belongs to a scope. A bridge supplies it (for example from tenant membership).
/// Without one, nobody is a member, so nobody is granted anything.
/// </summary>
public interface IAccessMembership
{
    /// <summary>Null when the user does not belong to the scope.</summary>
    Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default);
}