namespace CoreKit.Tenant.Abstractions;

/// <summary>
/// Who is making this request. The identity module provides it from the validated token, so it never
/// touches the database. It knows nothing about tenants, roles or permissions.
/// </summary>
public interface ITenantCurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}