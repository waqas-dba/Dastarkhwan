namespace CoreKit.Access.Persistence;

/// <summary>
/// Access-owned persistence for the tenant membership event stream: each tenant's applied-sequence cursor and
/// the set of event ids already applied. Both are used by the consumer and the snapshot installer.
/// </summary>
internal interface IAccessMembershipStreamRepository
{
    /// <summary>Tracked. The consumer and installer advance its sequence, so it doubles as a concurrency token.</summary>
    Task<AccessMembershipStreamCursor?> FindCursorAsync(Guid tenantId, CancellationToken ct = default);

    void AddCursor(AccessMembershipStreamCursor cursor);

    /// <summary>Read-only. Null when the event id has not been applied.</summary>
    Task<AccessProcessedEvent?> FindProcessedAsync(Guid eventId, CancellationToken ct = default);

    void AddProcessed(AccessProcessedEvent processed);

    /// <summary>Read-only. Used to distinguish a duplicate from a sequence reused by a different event.</summary>
    Task<AccessProcessedEvent?> FindProcessedBySequenceAsync(
        Guid tenantId, long sequence, CancellationToken ct = default);
}

internal sealed class AccessMembershipStreamRepository : IAccessMembershipStreamRepository
{
    private readonly AccessDbContext _db;

    public AccessMembershipStreamRepository(AccessDbContext db) => _db = db;

    public Task<AccessMembershipStreamCursor?> FindCursorAsync(Guid tenantId, CancellationToken ct = default)
        => _db.MembershipStreamCursors.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);

    public void AddCursor(AccessMembershipStreamCursor cursor) => _db.MembershipStreamCursors.Add(cursor);

    public Task<AccessProcessedEvent?> FindProcessedAsync(Guid eventId, CancellationToken ct = default)
        => _db.ProcessedEvents.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public void AddProcessed(AccessProcessedEvent processed) => _db.ProcessedEvents.Add(processed);

    public Task<AccessProcessedEvent?> FindProcessedBySequenceAsync(
        Guid tenantId, long sequence, CancellationToken ct = default)
        => _db.ProcessedEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Sequence == sequence, ct);
}