using CoreKit.SharedKernel;

namespace CoreKit.IAM.Services;

/// <summary>
/// Who is making this request? Read from the validated token, so it never touches the database.
/// It deliberately knows nothing about tenants, stores or restaurants. UserId and IsAuthenticated come
/// from the shared kernel's ICurrentUser, so other modules can read them without referencing IAM.
/// </summary>
public interface ICurrentUserService : ICurrentUser
{
    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlyCollection<string> Permissions { get; }
}