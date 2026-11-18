namespace CoreKit.Identity.Services;

/// <summary>
/// Who is making this request? Read from the validated token, so it never touches the database.
/// It knows nothing about tenants, stores or restaurants. UserId and IsAuthenticated come from
/// the validated JWT; Roles and Permissions are owned by Access.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    string? Email { get; }
}