using System.Text.Json;

namespace CoreKit.Access.Services;

/// <summary>
/// Access's durable consumer of Tenant membership and lifecycle events. It advances one per-tenant cursor,
/// records each applied event id, and rejects gaps instead of skipping them. A redelivery is a duplicate.
/// </summary>
internal sealed class AccessMembershipEventConsumer : IAccessMembershipEventConsumer
{
    private const int MaxContentionAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IAccessMembershipStreamRepository _stream;
    private readonly IAccessMembershipProjectionRepository _projections;
    private readonly IAccessTenantStateRepository _tenantStates;
    private readonly IAccessUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly ILogger<AccessMembershipEventConsumer> _logger;
    private readonly IAccessCache? _cache;

    public AccessMembershipEventConsumer(
        IAccessMembershipStreamRepository stream,
        IAccessMembershipProjectionRepository projections,
        IAccessTenantStateRepository tenantStates,
        IAccessUnitOfWork unitOfWork,
        TimeProvider time,
        ILogger<AccessMembershipEventConsumer> logger,
        IAccessCache? cache = null)
    {
        _stream = stream;
        _projections = projections;
        _tenantStates = tenantStates;
        _unitOfWork = unitOfWork;
        _time = time;
        _logger = logger;
        _cache = cache;
    }

    public async Task<AccessMembershipEventResult> ApplyAsync(
        AccessMembershipEventEnvelope envelope, CancellationToken ct = default)
    {
        if (Validate(envelope, out var parsed) is { } invalid)
            return invalid;

        for (var attempt = 1; attempt <= MaxContentionAttempts; attempt++)
        {
            var processed = await _stream.FindProcessedAsync(envelope.EventId, ct);
            if (processed is not null)
            {
                return processed.TenantId == envelope.TenantId && processed.Sequence == envelope.Sequence
                    ? AccessMembershipEventResult.Duplicate("The event was already applied.")
                    : AccessMembershipEventResult.Rejected("The event id was already used for a different event.");
            }

            var cursor = await _stream.FindCursorAsync(envelope.TenantId, ct);
            var last = cursor?.LastAppliedSequence ?? 0;

            if (envelope.Sequence <= last)
            {
                var holder = await _stream.FindProcessedBySequenceAsync(envelope.TenantId, envelope.Sequence, ct);
                return holder is null
                    ? AccessMembershipEventResult.Duplicate(
                        "The sequence was already applied (history no longer held or covered by a snapshot).")
                    : AccessMembershipEventResult.Rejected("The sequence was already applied by a different event.");
            }

            if (envelope.Sequence > last + 1)
            {
                // A gap is never skipped: applying a later event first could let a stale state win.
                return AccessMembershipEventResult.RetryLater(
                    $"Sequence gap for tenant {envelope.TenantId}: expected {last + 1}, received {envelope.Sequence}.");
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var state = await _tenantStates.FindAsync(envelope.TenantId, ct);

            // Deleted is terminal: nothing may follow it.
            AccessMembershipEventResult? rejected = state?.Status == AccessTenantStatuses.Deleted
                ? AccessMembershipEventResult.Rejected("The tenant is deleted.")
                : parsed.Membership is { } membership
                    ? await ApplyMembershipAsync(envelope, membership, now, ct)
                    : ApplyLifecycle(envelope, parsed.Lifecycle!, state, now);

            if (rejected is { } rejection)
            {
                _logger.LogError(
                    "Event {EventId} (tenant {TenantId}, sequence {Sequence}, {EventType}) was rejected: {Reason}",
                    envelope.EventId, envelope.TenantId, envelope.Sequence, envelope.EventType, rejection.Reason);
                return rejection;
            }

            if (cursor is null)
            {
                _stream.AddCursor(new AccessMembershipStreamCursor
                {
                    TenantId = envelope.TenantId,
                    LastAppliedSequence = envelope.Sequence,
                    UpdatedAt = now
                });
            }
            else
            {
                cursor.LastAppliedSequence = envelope.Sequence;
                cursor.UpdatedAt = now;
            }

            _stream.AddProcessed(new AccessProcessedEvent
            {
                EventId = envelope.EventId,
                TenantId = envelope.TenantId,
                Sequence = envelope.Sequence,
                EventType = envelope.EventType,
                ProcessedAt = now
            });

            // One SaveChanges: projection or state, cursor and dedup record commit together or not at all.
            if (await _unitOfWork.SaveAsync(ct) == SaveOutcome.Saved)
            {
                // Only this server's cache can be cleared here. Other servers rely on the documented
                // CacheSeconds bound, which is part of MaxRevocationDelaySeconds.
                if (parsed.Membership is { } applied)
                    _cache?.InvalidateUser(AccessScope.ForTenant(envelope.TenantId), applied.UserId);
                else
                    _cache?.InvalidateScope(AccessScope.ForTenant(envelope.TenantId));

                return AccessMembershipEventResult.Applied();
            }

            // A concurrent consumer won (unique index or cursor concurrency check). The unit of work cleared
            // the tracker; loop to re-read and re-decide, which usually ends as Duplicate.
        }

        return AccessMembershipEventResult.RetryLater("Contention while applying the event; retry.");
    }

    private static AccessMembershipEventResult? Validate(
        AccessMembershipEventEnvelope? envelope, out ParsedEvent parsed)
    {
        parsed = null!;

        if (envelope is null)
            return AccessMembershipEventResult.Rejected("No event.");

        if (envelope.EventId == Guid.Empty || envelope.TenantId == Guid.Empty || envelope.Sequence < 1)
            return AccessMembershipEventResult.Rejected("The event identity, tenant or sequence is invalid.");

        if (envelope.SchemaVersion != AccessMembershipEventTypes.SupportedSchemaVersion)
            return AccessMembershipEventResult.Rejected($"Unsupported schema version {envelope.SchemaVersion}.");

        switch (envelope.EventType)
        {
            case AccessMembershipEventTypes.MemberAdded:
            case AccessMembershipEventTypes.MemberReactivated:
            case AccessMembershipEventTypes.MemberRemoved:
            case AccessMembershipEventTypes.MemberOwnershipChanged:
                return ValidateMembership(envelope, out parsed);

            case AccessMembershipEventTypes.TenantCreated:
            case AccessMembershipEventTypes.TenantStatusChanged:
            case AccessMembershipEventTypes.TenantDeleted:
                return ValidateLifecycle(envelope, out parsed);

            default:
                return AccessMembershipEventResult.Rejected($"Unsupported event type '{envelope.EventType}'.");
        }
    }

    private static AccessMembershipEventResult? ValidateMembership(
        AccessMembershipEventEnvelope envelope, out ParsedEvent parsed)
    {
        parsed = null!;

        MembershipPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<MembershipPayload>(envelope.PayloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            return AccessMembershipEventResult.Rejected("The payload is not valid JSON.");
        }

        if (payload is null || payload.UserId == Guid.Empty || payload.MembershipGeneration < 1)
            return AccessMembershipEventResult.Rejected("The payload is incomplete.");

        if (payload.TenantId != envelope.TenantId
            || envelope.SubjectUserId != payload.UserId
            || envelope.MembershipGeneration != payload.MembershipGeneration)
            return AccessMembershipEventResult.Rejected("The payload does not match the event envelope.");

        if ((envelope.EventType == AccessMembershipEventTypes.MemberRemoved) == payload.IsActive)
            return AccessMembershipEventResult.Rejected("The payload's active flag contradicts the event type.");

        parsed = new ParsedEvent(payload, null);
        return null;
    }

    private static AccessMembershipEventResult? ValidateLifecycle(
        AccessMembershipEventEnvelope envelope, out ParsedEvent parsed)
    {
        parsed = null!;

        LifecyclePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LifecyclePayload>(envelope.PayloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            return AccessMembershipEventResult.Rejected("The payload is not valid JSON.");
        }

        if (payload is null
            || payload.TenantId != envelope.TenantId
            || envelope.SubjectUserId is not null
            || envelope.MembershipGeneration is not null
            || !AccessTenantStatuses.IsKnown(payload.Status))
            return AccessMembershipEventResult.Rejected("The lifecycle payload is invalid or does not match the envelope.");

        var valid = envelope.EventType switch
        {
            AccessMembershipEventTypes.TenantCreated
                => payload.FromStatus is null && payload.Status != AccessTenantStatuses.Deleted,
            AccessMembershipEventTypes.TenantStatusChanged
                => AccessTenantStatuses.IsKnown(payload.FromStatus)
                   && payload.Status != AccessTenantStatuses.Deleted
                   && payload.Status != payload.FromStatus,
            _ => payload.Status == AccessTenantStatuses.Deleted
        };

        if (!valid)
            return AccessMembershipEventResult.Rejected("The lifecycle transition is not valid for this event type.");

        parsed = new ParsedEvent(null, payload);
        return null;
    }

    private async Task<AccessMembershipEventResult?> ApplyMembershipAsync(
        AccessMembershipEventEnvelope envelope, MembershipPayload payload, DateTime now, CancellationToken ct)
    {
        var existing = await _projections.FindAsync(envelope.TenantId, payload.UserId, ct);

        // Generation never moves backwards. This is what stops a stale event restoring a revoked member.
        if (existing is not null && payload.MembershipGeneration < existing.MembershipGeneration)
        {
            return AccessMembershipEventResult.Rejected(
                $"Stale generation {payload.MembershipGeneration}; the projection is at {existing.MembershipGeneration}.");
        }

        switch (envelope.EventType)
        {
            case AccessMembershipEventTypes.MemberAdded:
                if (existing is not null)
                    return AccessMembershipEventResult.Rejected("The member is already projected.");
                _projections.Add(NewRow(envelope, payload, AccessMembershipProjectionStatus.Active, now));
                return null;

            case AccessMembershipEventTypes.MemberReactivated:
                if (existing is null)
                {
                    _projections.Add(NewRow(envelope, payload, AccessMembershipProjectionStatus.Active, now));
                    return null;
                }

                if (existing.Status != AccessMembershipProjectionStatus.Removed
                    || payload.MembershipGeneration <= existing.MembershipGeneration)
                    return AccessMembershipEventResult.Rejected("A reactivation needs a removed member and a higher generation.");

                Update(existing, envelope, payload, AccessMembershipProjectionStatus.Active, now);
                return null;

            case AccessMembershipEventTypes.MemberRemoved:
                if (existing is null)
                {
                    // Fail closed: record the removal even if the earlier add was never projected.
                    _projections.Add(NewRow(envelope, payload, AccessMembershipProjectionStatus.Removed, now));
                    return null;
                }

                if (existing.Status != AccessMembershipProjectionStatus.Active
                    || payload.MembershipGeneration <= existing.MembershipGeneration)
                    return AccessMembershipEventResult.Rejected("A removal needs an active member and a higher generation.");

                Update(existing, envelope, payload, AccessMembershipProjectionStatus.Removed, now);
                return null;

            default:
                // Ownership changes do not alter Access state: Tenant ownership never implies Access ownership.
                // The event still advances the cursor so the stream stays contiguous.
                return existing is not null && payload.MembershipGeneration != existing.MembershipGeneration
                    ? AccessMembershipEventResult.Rejected("An ownership change must not change the generation.")
                    : null;
        }
    }

    private AccessMembershipEventResult? ApplyLifecycle(
        AccessMembershipEventEnvelope envelope, LifecyclePayload payload, AccessTenantState? state, DateTime now)
    {
        switch (envelope.EventType)
        {
            case AccessMembershipEventTypes.TenantCreated:
                if (state is not null)
                    return AccessMembershipEventResult.Rejected("The tenant state already exists.");
                _tenantStates.Add(NewState(envelope.TenantId, payload.Status!, now));
                return null;

            case AccessMembershipEventTypes.TenantStatusChanged:
                if (state is null)
                {
                    // Tolerant: the state was never created here (for example before a bootstrap).
                    _tenantStates.Add(NewState(envelope.TenantId, payload.Status!, now));
                    return null;
                }

                if (!string.Equals(state.Status, payload.FromStatus, StringComparison.Ordinal))
                    return AccessMembershipEventResult.Rejected(
                        $"The event leaves status {payload.FromStatus} but Access holds {state.Status}.");

                state.Status = payload.Status!;
                state.UpdatedAt = now;
                return null;

            default: // tenant deleted
                if (state is null)
                    _tenantStates.Add(NewState(envelope.TenantId, AccessTenantStatuses.Deleted, now));
                else
                {
                    state.Status = AccessTenantStatuses.Deleted;
                    state.UpdatedAt = now;
                }

                return null;
        }
    }

    private static AccessTenantState NewState(Guid tenantId, string status, DateTime now)
        => new() { TenantId = tenantId, Status = status, UpdatedAt = now };

    private static AccessMembershipProjection NewRow(
        AccessMembershipEventEnvelope envelope, MembershipPayload payload,
        AccessMembershipProjectionStatus status, DateTime now) => new()
    {
        TenantId = envelope.TenantId,
        UserId = payload.UserId,
        Status = status,
        MembershipGeneration = payload.MembershipGeneration,
        LastAppliedSequence = envelope.Sequence,
        UpdatedAt = now
    };

    private static void Update(
        AccessMembershipProjection row, AccessMembershipEventEnvelope envelope, MembershipPayload payload,
        AccessMembershipProjectionStatus status, DateTime now)
    {
        row.Status = status;
        row.MembershipGeneration = payload.MembershipGeneration;
        row.LastAppliedSequence = envelope.Sequence;
        row.UpdatedAt = now;
    }

    private sealed record ParsedEvent(MembershipPayload? Membership, LifecyclePayload? Lifecycle);

    private sealed record MembershipPayload(
        Guid TenantId, Guid UserId, long MembershipGeneration, bool IsOwner, bool IsActive, DateTime OccurredAt);

    private sealed record LifecyclePayload(Guid TenantId, string? FromStatus, string? Status, DateTime OccurredAt);
}