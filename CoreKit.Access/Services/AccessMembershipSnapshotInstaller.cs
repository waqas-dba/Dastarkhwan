namespace CoreKit.Access.Services;

/// <summary>
/// Installs an authoritative Tenant snapshot into Access: members, lifecycle state and the stream cursor, in
/// one transaction. Access never moves backwards; a snapshot older than the cursor is ignored.
/// </summary>
internal sealed class AccessMembershipSnapshotInstaller : IAccessMembershipSnapshotInstaller
{
    private const int MaxContentionAttempts = 3;

    private readonly IAccessMembershipStreamRepository _stream;
    private readonly IAccessMembershipProjectionRepository _projections;
    private readonly IAccessTenantStateRepository _tenantStates;
    private readonly IAccessUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly IAccessCache? _cache;

    public AccessMembershipSnapshotInstaller(
        IAccessMembershipStreamRepository stream,
        IAccessMembershipProjectionRepository projections,
        IAccessTenantStateRepository tenantStates,
        IAccessUnitOfWork unitOfWork,
        TimeProvider time,
        IAccessCache? cache = null)
    {
        _stream = stream;
        _projections = projections;
        _tenantStates = tenantStates;
        _unitOfWork = unitOfWork;
        _time = time;
        _cache = cache;
    }

    public async Task<AccessSnapshotOutcome> InstallAsync(
        AccessMembershipSnapshot snapshot, CancellationToken ct = default)
    {
        if (!IsValid(snapshot))
            return AccessSnapshotOutcome.Rejected;

        var desired = snapshot.Members.ToDictionary(m => m.UserId);

        for (var attempt = 1; attempt <= MaxContentionAttempts; attempt++)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var cursor = await _stream.FindCursorAsync(snapshot.TenantId, ct);
            var last = cursor?.LastAppliedSequence ?? 0;

            // Access never moves backwards: an older snapshot must not overwrite newer applied state.
            if (snapshot.HeadSequence < last)
                return AccessSnapshotOutcome.IgnoredStale;

            var state = await _tenantStates.FindAsync(snapshot.TenantId, ct);
            if (state?.Status == AccessTenantStatuses.Deleted)
                return AccessSnapshotOutcome.Rejected;

            var rows = await _projections.ListByTenantForUpdateAsync(snapshot.TenantId, ct);
            var changed = false;

            foreach (var row in rows)
            {
                if (!desired.TryGetValue(row.UserId, out var want))
                {
                    _projections.Remove(row);
                    changed = true;
                    continue;
                }

                var status = want.IsActive
                    ? AccessMembershipProjectionStatus.Active
                    : AccessMembershipProjectionStatus.Removed;

                if (row.Status != status || row.MembershipGeneration != want.MembershipGeneration)
                {
                    row.Status = status;
                    row.MembershipGeneration = want.MembershipGeneration;
                    row.LastAppliedSequence = snapshot.HeadSequence;
                    row.UpdatedAt = now;
                    changed = true;
                }
            }

            var present = rows.Select(r => r.UserId).ToHashSet();
            foreach (var want in snapshot.Members.Where(m => !present.Contains(m.UserId)))
            {
                _projections.Add(new AccessMembershipProjection
                {
                    TenantId = snapshot.TenantId,
                    UserId = want.UserId,
                    Status = want.IsActive
                        ? AccessMembershipProjectionStatus.Active
                        : AccessMembershipProjectionStatus.Removed,
                    MembershipGeneration = want.MembershipGeneration,
                    LastAppliedSequence = snapshot.HeadSequence,
                    UpdatedAt = now
                });
                changed = true;
            }

            if (state is null)
            {
                _tenantStates.Add(new AccessTenantState
                {
                    TenantId = snapshot.TenantId, Status = snapshot.TenantStatus, UpdatedAt = now
                });
                changed = true;
            }
            else if (!string.Equals(state.Status, snapshot.TenantStatus, StringComparison.Ordinal))
            {
                state.Status = snapshot.TenantStatus;
                state.UpdatedAt = now;
                changed = true;
            }

            var advance = cursor is null || snapshot.HeadSequence > last;
            if (!changed && !advance)
                return AccessSnapshotOutcome.Unchanged;

            if (cursor is null)
            {
                _stream.AddCursor(new AccessMembershipStreamCursor
                {
                    TenantId = snapshot.TenantId,
                    LastAppliedSequence = snapshot.HeadSequence,
                    UpdatedAt = now
                });
            }
            else if (advance)
            {
                cursor.LastAppliedSequence = snapshot.HeadSequence;
                cursor.UpdatedAt = now;
            }

            // One SaveChanges for members, lifecycle state and cursor. The cursor is a concurrency token, so a
            // consumer applying an event at the same moment makes one side lose and re-read.
            if (await _unitOfWork.SaveAsync(ct) == SaveOutcome.Saved)
            {
                _cache?.InvalidateScope(AccessScope.ForTenant(snapshot.TenantId));
                return advance ? AccessSnapshotOutcome.Installed : AccessSnapshotOutcome.Repaired;
            }
        }

        return AccessSnapshotOutcome.RetryLater;
    }

    private static bool IsValid(AccessMembershipSnapshot? snapshot)
        => snapshot is not null
           && snapshot.TenantId != Guid.Empty
           && AccessTenantStatuses.IsKnown(snapshot.TenantStatus)
           && snapshot.TenantStatus != AccessTenantStatuses.Deleted
           && snapshot.HeadSequence >= 0
           && snapshot.Members is not null
           && snapshot.Members.All(m => m.UserId != Guid.Empty && m.MembershipGeneration >= 1)
           && snapshot.Members.Select(m => m.UserId).Distinct().Count() == snapshot.Members.Count;
}

/// <summary>Read-only view of the Access projection, used by Tenant's recovery and reconciliation.</summary>
internal sealed class AccessMembershipProjectionReader : IAccessMembershipProjectionReader
{
    private readonly IAccessMembershipStreamRepository _stream;
    private readonly IAccessMembershipProjectionRepository _projections;
    private readonly IAccessTenantStateRepository _tenantStates;

    public AccessMembershipProjectionReader(
        IAccessMembershipStreamRepository stream,
        IAccessMembershipProjectionRepository projections,
        IAccessTenantStateRepository tenantStates)
    {
        _stream = stream;
        _projections = projections;
        _tenantStates = tenantStates;
    }

    public async Task<AccessTenantProjectionView?> GetTenantViewAsync(Guid tenantId, CancellationToken ct = default)
    {
        var cursor = await _stream.FindCursorAsync(tenantId, ct);
        var status = await _tenantStates.GetStatusAsync(tenantId, ct);
        var rows = await _projections.ListByTenantAsync(tenantId, ct);

        if (cursor is null && status is null && rows.Count == 0)
            return null;

        return new AccessTenantProjectionView(
            tenantId,
            cursor?.LastAppliedSequence ?? 0,
            status,
            rows.Select(r => new AccessProjectedMember(
                r.UserId, r.Status == AccessMembershipProjectionStatus.Active, r.MembershipGeneration)).ToList());
    }

    public Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken ct = default)
        => _tenantStates.ListTenantIdsAsync(ct);
}