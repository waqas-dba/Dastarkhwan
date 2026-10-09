using CoreKit.IAM.Entities;
using CoreKit.IAM.Models;

namespace CoreKit.IAM.Services;

public static class IamMapper
{
    /// <summary>The user must be loaded with UserRoles and each UserRole's Role.</summary>
    public static UserDto ToDto(User user, IReadOnlyCollection<string>? permissions = null) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        IsActive = user.IsActive,
        EmailConfirmed = user.EmailConfirmed,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        Roles = user.UserRoles.Select(ur => ur.Role.Name).OrderBy(name => name).ToList(),
        Permissions = permissions?.ToList() ?? new List<string>()
    };
}