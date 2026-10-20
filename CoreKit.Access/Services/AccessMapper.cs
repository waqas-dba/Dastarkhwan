namespace CoreKit.Access.Services;

internal static class AccessMapper
{
    /// <summary>The role must be loaded with its Permissions.</summary>
    public static AccessRoleDto ToDto(AccessRole role, int memberCount) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        Permissions = role.Permissions.Select(p => p.Permission).Order(StringComparer.Ordinal).ToList(),
        MemberCount = memberCount,
        CreatedAt = role.CreatedAt,
        UpdatedAt = role.UpdatedAt
    };
}