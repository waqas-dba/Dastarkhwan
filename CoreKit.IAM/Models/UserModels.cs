namespace CoreKit.IAM.Models;

public sealed class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public bool IsActive { get; set; } = true;

    public List<Guid> RoleIds { get; set; } = new();
}

/// <summary>Every property is optional. A property that is not sent stays unchanged.</summary>
public sealed class UpdateUserRequest
{
    public string? Email { get; set; }

    /// <summary>An empty string clears the display name.</summary>
    public string? DisplayName { get; set; }

    public bool? IsActive { get; set; }

    /// <summary>An administrator can set a new password. All of the user's sessions are ended.</summary>
    public string? Password { get; set; }
}

public sealed class SetUserRolesRequest
{
    /// <summary>The complete list of roles the user should have afterwards.</summary>
    public List<Guid> RoleIds { get; set; } = new();
}

public sealed class UserListQuery
{
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
