using CoreKit.IAM.Entities;

namespace CoreKit.IAM.Interfaces;

/// <summary>What a user may actually do right now.</summary>
public sealed record UserAccess(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public interface IAccessResolver
{
    /// <summary>The user must be loaded with UserRoles and each UserRole's Role.</summary>
    Task<UserAccess> ResolveAsync(User user, CancellationToken ct = default);
}