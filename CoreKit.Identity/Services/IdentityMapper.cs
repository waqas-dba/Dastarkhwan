using CoreKit.Identity.Entities;
using CoreKit.Identity.Models;

namespace CoreKit.Identity.Services;

public static class IdentityMapper
{
    public static UserDto ToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Mobile = user.Mobile,
        DisplayName = user.DisplayName,
        IsActive = user.IsActive,
        EmailConfirmed = user.EmailConfirmed,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };
}
