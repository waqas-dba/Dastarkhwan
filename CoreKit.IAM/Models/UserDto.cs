using System;
using System.Collections.Generic;
using System.Text;

namespace CoreKit.IAM.Models;


/// <summary>What the API returns about a user. It has no password hash, by design.</summary>
public sealed class UserDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string? DisplayName { get; set; }

    public bool IsActive { get; set; }

    public bool EmailConfirmed { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<string> Roles { get; set; } = new();

    /// <summary>Filled in for login and /me. List endpoints leave it empty to stay fast.</summary>
    public List<string> Permissions { get; set; } = new();
}