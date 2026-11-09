namespace CoreKit.Access.Abstractions;

/// <summary>
/// Who is making this request. Access resolves this from the request context (for example a JWT claim),
/// never from the database. It knows nothing about tenants, roles or permissions.
/// </summary>
public interface IAccessCurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}
