namespace CoreKit.Access.Abstractions;

/// <summary>
/// Who is making this request. The identity module provides it from the validated token, so it never
/// touches the database. It knows nothing about tenants, roles or permissions.
/// </summary>
public interface ICurrentUser : IAccessCurrentUser
{
    /// <summary>The permissions the caller holds, if known.</summary>
    IReadOnlyCollection<string> Permissions { get; }
}