using Iam.Core.Identity;
using Iam.Core.Results;
using System.Data;
using System.Net.Mail;

namespace Iam.Core.Entities;

public sealed class User
{
    private readonly List<UserRole> _userRoles = new();

    private User() { } // used by EF Core

    public Guid Id { get; private set; }

    /// <summary>Normalised (trimmed, lower-case). Null when the user signs in only with a phone.</summary>
    public string? Email { get; private set; }

    /// <summary>E.164, for example +923001234567. Null when the user signs in only with an email.</summary>
    public string? Phone { get; private set; }

    public bool EmailConfirmed { get; private set; }
    public bool PhoneConfirmed { get; private set; }

    public string DisplayName { get; private set; } = default!;
    public string? PasswordHash { get; private set; }
    public UserStatus Status { get; private set; }

    /// <summary>
    /// Changes whenever something happens that must end existing sign-ins
    /// (password change, account disabled). Access tokens carry it and are rejected when it no longer matches.
    /// </summary>
    public Guid SecurityStamp { get; private set; }

    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public DateTimeOffset? LastLoginAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;

    public static Result<User> Create(
        string displayName,
        EmailAddress? email,
        PhoneNumber? phone,
        string passwordHash,
        DateTimeOffset now)
    {
        var name = displayName?.Trim();

        if (string.IsNullOrEmpty(name))
            return Result.Failure<User>(IamErrors.Users.DisplayNameRequired);

        if (name.Length > 200)
            return Result.Failure<User>(IamErrors.Users.DisplayNameTooLong);

        if (email is null && phone is null)
            return Result.Failure<User>(IamErrors.Users.ContactRequired);

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("A password hash is required.", nameof(passwordHash));

        return Result.Success(new User
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            Email = email?.Value,
            Phone = phone?.Value,
            PasswordHash = passwordHash,
            Status = UserStatus.Active,
            SecurityStamp = Guid.NewGuid(),
            CreatedAtUtc = now
        });
    }

    public bool IsLockedOut(DateTimeOffset now)
        => LockoutEndUtc.HasValue && LockoutEndUtc.Value > now;

    public bool CanSignIn(DateTimeOffset now)
        => Status == UserStatus.Active && !IsLockedOut(now);

    public void RegisterFailedLogin(DateTimeOffset now, int maxAttempts, TimeSpan lockoutDuration)
    {
        AccessFailedCount++;

        if (AccessFailedCount >= maxAttempts)
        {
            LockoutEndUtc = now + lockoutDuration;
            AccessFailedCount = 0;
        }

        UpdatedAtUtc = now;
    }

    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        AccessFailedCount = 0;
        LockoutEndUtc = null;
        LastLoginAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void ChangePasswordHash(string newPasswordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("A password hash is required.", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        SecurityStamp = Guid.NewGuid();
        AccessFailedCount = 0;
        LockoutEndUtc = null;
        UpdatedAtUtc = now;
    }

    public void Disable(DateTimeOffset now)
    {
        if (Status == UserStatus.Disabled) return;

        Status = UserStatus.Disabled;
        SecurityStamp = Guid.NewGuid();
        UpdatedAtUtc = now;
    }

    public void Enable(DateTimeOffset now)
    {
        if (Status == UserStatus.Active) return;

        Status = UserStatus.Active;
        UpdatedAtUtc = now;
    }

    public void ConfirmEmail(DateTimeOffset now)
    {
        if (Email is null || EmailConfirmed) return;

        EmailConfirmed = true;
        UpdatedAtUtc = now;
    }

    public void ConfirmPhone(DateTimeOffset now)
    {
        if (Phone is null || PhoneConfirmed) return;

        PhoneConfirmed = true;
        UpdatedAtUtc = now;
    }

    /// <summary>Returns false when the user already has the role.</summary>
    public bool AssignRole(Role role, DateTimeOffset now)
    {
        if (_userRoles.Any(r => r.RoleId == role.Id)) return false;

        _userRoles.Add(new UserRole(Id, role.Id, now));
        UpdatedAtUtc = now;
        return true;
    }

    /// <summary>Returns false when the user did not have the role.</summary>
    public bool RemoveRole(Guid roleId, DateTimeOffset now)
    {
        var link = _userRoles.FirstOrDefault(r => r.RoleId == roleId);
        if (link is null) return false;

        _userRoles.Remove(link);
        UpdatedAtUtc = now;
        return true;
    }
}