using CoreKit.Identity.Entities;
using CoreKit.Identity.Models;

namespace CoreKit.Identity.Services;

public static class IdentityMapper
{
    /// <summary>Roles and permissions come from Access (the platform scope); Identity no longer owns them.</summary>
    public static UserDto ToDto(
        User user,
        IReadOnlyCollection<string>? roles = null,
        IReadOnlyCollection<string>? permissions = null) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Mobile = user.Mobile,
        DisplayName = user.DisplayName,
        IsActive = user.IsActive,
        EmailConfirmed = user.EmailConfirmed,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        Roles = roles?.OrderBy(name => name, StringComparer.Ordinal).ToList() ?? new List<string>(),
        Permissions = permissions?.OrderBy(name => name, StringComparer.Ordinal).ToList() ?? new List<string>()
    };
}