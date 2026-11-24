namespace CoreKit.Access.Contracts;

/// <summary>Audit entry DTO.</summary>
public sealed class AccessAuditEntryDto
{
    public Guid Id { get; set; }

    public string Action { get; set; } = null!;

    public Guid? ActorUserId { get; set; }

    public string ActorType { get; set; } = null!;

    public string? ActorSource { get; set; }

    public string? Details { get; set; }

    public DateTime OccurredAt { get; set; }
}

/// <summary>Paged result wrapper.</summary>
public sealed class AccessPagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>The history of role and permission changes in the current scope. Needs access.audit.read.</summary>
public interface IAccessAuditLog
{
    /// <summary>Newest first.</summary>
    Task<AccessResult<AccessPagedResult<AccessAuditEntryDto>>> ListAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default);
}