using System;

namespace CoreKit.Identity.Models;


/// <summary>What the API returns about a user. It has no password hash, by design.</summary>
public sealed class UserDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string? Mobile { get; set; }

    public string? DisplayName { get; set; }

    public bool IsActive { get; set; }

    public bool EmailConfirmed { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}