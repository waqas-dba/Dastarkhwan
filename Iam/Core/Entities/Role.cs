using Iam.Core.Results;

namespace Iam.Core.Entities;

public sealed class Role
{
    private readonly List<RolePermission> _rolePermissions = new();

    private Role() { } // used by EF Core

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;

    /// <summary>Upper-case copy of Name, used for a case-insensitive unique index.</summary>
    public string NormalizedName { get; private set; } = default!;

    public string? Description { get; private set; }

    /// <summary>Built-in roles cannot be renamed or deleted.</summary>
    public bool IsSystem { get; private set; }

    /// <summary>Goes up every time the role's permissions change, so caches know to refresh.</summary>
    public int Version { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;

    public static Result<Role> Create(string name, string? description, bool isSystem, DateTimeOffset now)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
            return Result.Failure<Role>(IamErrors.Roles.NameRequired);

        if (trimmed.Length > 100)
            return Result.Failure<Role>(IamErrors.Roles.NameTooLong);

        return Result.Success(new Role
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            NormalizedName = trimmed.ToUpperInvariant(),
            Description = description?.Trim(),
            IsSystem = isSystem,
            Version = 1,
            CreatedAtUtc = now
        });
    }

    public Result Rename(string newName, DateTimeOffset now)
    {
        if (IsSystem) return Result.Failure(IamErrors.Roles.SystemRoleLocked);

        var trimmed = newName?.Trim();

        if (string.IsNullOrEmpty(trimmed))
            return Result.Failure(IamErrors.Roles.NameRequired);

        if (trimmed.Length > 100)
            return Result.Failure(IamErrors.Roles.NameTooLong);

        Name = trimmed;
        NormalizedName = trimmed.ToUpperInvariant();
        UpdatedAtUtc = now;
        return Result.Success();
    }

    public void UpdateDescription(string? description, DateTimeOffset now)
    {
        Description = description?.Trim();
        UpdatedAtUtc = now;
    }

    public Result CanBeDeleted()
        => IsSystem ? Result.Failure(IamErrors.Roles.SystemRoleLocked) : Result.Success();

    /// <summary>Returns false when the role already has the permission.</summary>
    public bool GrantPermission(Guid permissionId, DateTimeOffset now)
    {
        if (_rolePermissions.Any(p => p.PermissionId == permissionId)) return false;

        _rolePermissions.Add(new RolePermission(Id, permissionId, now));
        Version++;
        UpdatedAtUtc = now;
        return true;
    }

    /// <summary>Returns false when the role did not have the permission.</summary>
    public bool RevokePermission(Guid permissionId, DateTimeOffset now)
    {
        var link = _rolePermissions.FirstOrDefault(p => p.PermissionId == permissionId);
        if (link is null) return false;

        _rolePermissions.Remove(link);
        Version++;
        UpdatedAtUtc = now;
        return true;
    }
}