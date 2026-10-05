using System.Text.RegularExpressions;
using Iam.Core.Results;

namespace Iam.Core.Entities;

public sealed partial class Permission
{
    [GeneratedRegex(@"^[a-z][a-z0-9_-]*(\.[a-z][a-z0-9_-]*)+$")]
    private static partial Regex CodePattern();

    private Permission() { } // used by EF Core

    public Guid Id { get; private set; }

    /// <summary>For example "orders.view" or "iam.users.view".</summary>
    public string Code { get; private set; } = default!;

    /// <summary>The first word of the code ("orders", "iam"), used to group permissions in screens.</summary>
    public string Group { get; private set; } = default!;

    public string? Description { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<Permission> Create(string code, string? description, DateTimeOffset now)
    {
        var normalized = code?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized) || normalized.Length > 150 || !CodePattern().IsMatch(normalized))
            return Result.Failure<Permission>(IamErrors.Permissions.InvalidCode);

        return Result.Success(new Permission
        {
            Id = Guid.NewGuid(),
            Code = normalized,
            Group = normalized[..normalized.IndexOf('.')],
            Description = description?.Trim(),
            CreatedAtUtc = now
        });
    }

    public void UpdateDescription(string? description)
        => Description = description?.Trim();
}