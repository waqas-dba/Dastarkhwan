namespace CoreKit.Access.Services;

internal sealed class AccessAuditLog : IAccessAuditLog
{
    private readonly AccessGuard _guard;
    private readonly IAccessAuditRepository _audit;

    public AccessAuditLog(AccessGuard guard, IAccessAuditRepository audit)
    {
        _guard = guard;
        _audit = audit;
    }

    public async Task<AccessResult<AccessPagedResult<AccessAuditEntryDto>>> ListAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var call = await _guard.RequireAsync(AccessPermissionNames.AuditRead, ct);

        if (call.IsFailure)
            return call.Error!;

        var safePage = Math.Max(page, 1);
        var safeSize = Math.Clamp(pageSize, 1, 200);

        var (items, totalCount) = await _audit.ListAsync(call.Value.Scope, safePage, safeSize, ct);

        return AccessResult.Success(new AccessPagedResult<AccessAuditEntryDto>
        {
            Items = items.Select(e => new AccessAuditEntryDto
            {
                Id = e.Id,
                Action = e.Action,
                ActorUserId = e.ActorUserId,
                Details = e.Details,
                OccurredAt = e.OccurredAt
            }).ToList(),
            Page = safePage,
            PageSize = safeSize,
            TotalCount = totalCount
        });
    }
}