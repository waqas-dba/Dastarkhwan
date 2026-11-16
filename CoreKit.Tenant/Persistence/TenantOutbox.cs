using System.Text.Json;
using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Events;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.Tenant.Persistence;

/// <summary>
/// Stages an outbox message. It is saved by the same <c>SaveChanges</c> as the membership or lifecycle change,
/// so the change and its event commit together or not at all.
/// </summary>
public interface ITenantOutbox
{
    /// <summary>
    /// Stages one membership event. <paramref name="trackedTenant"/> must be tracked by the same context:
    /// this call assigns the next per-tenant sequence and rotates the tenant's concurrency stamp, which is
    /// what makes concurrent writers fail instead of issuing the same sequence.
    /// </summary>
    TenantOutboxMessage StageMembershipEvent(
        TenantEntity trackedTenant, string eventType, TenantMember member, DateTime occurredAt);

    /// <summary>
    /// Stages one tenant lifecycle event (created, status changed, deleted) with the same sequence and
    /// concurrency rules. A tenant being deleted in the same <c>SaveChanges</c> is still tracked, so it is
    /// accepted; the event outlives the tenant row because the outbox has no foreign key to it.
    /// </summary>
    TenantOutboxMessage StageLifecycleEvent(
        TenantEntity trackedTenant, string eventType, string? fromStatus, string status, DateTime occurredAt);
}

public sealed class TenantOutbox : ITenantOutbox
{
    private static readonly HashSet<string> MembershipEventTypes = new(StringComparer.Ordinal)
    {
        TenantOutboxEventTypes.MemberAdded,
        TenantOutboxEventTypes.MemberReactivated,
        TenantOutboxEventTypes.MemberRemoved,
        TenantOutboxEventTypes.MemberOwnershipChanged
    };

    private static readonly HashSet<string> LifecycleEventTypes = new(StringComparer.Ordinal)
    {
        TenantOutboxEventTypes.TenantCreated,
        TenantOutboxEventTypes.TenantStatusChanged,
        TenantOutboxEventTypes.TenantDeleted
    };

    private readonly TenantDbContext _db;

    public TenantOutbox(TenantDbContext db) => _db = db;

    public TenantOutboxMessage StageMembershipEvent(
        TenantEntity trackedTenant, string eventType, TenantMember member, DateTime occurredAt)
    {
        ArgumentNullException.ThrowIfNull(trackedTenant);
        ArgumentNullException.ThrowIfNull(member);

        if (!MembershipEventTypes.Contains(eventType))
            throw new ArgumentException($"'{eventType}' is not a membership event type.", nameof(eventType));

        if (trackedTenant.Id != member.TenantId)
            throw new InvalidOperationException("The member does not belong to the tenant being sequenced.");

        RequireTracked(trackedTenant);

        if (member.MembershipGeneration < 1)
            throw new InvalidOperationException("A membership generation must be at least 1.");

        var isActive = member.RemovedAt is null;
        if ((eventType == TenantOutboxEventTypes.MemberRemoved) == isActive)
            throw new InvalidOperationException(
                "Only a removed member may produce a removal event, and a removal event requires a removed member.");

        var payload = new TenantMembershipEventPayload(
            trackedTenant.Id, member.UserId, member.MembershipGeneration, member.IsOwner, isActive, occurredAt);

        return Enqueue(trackedTenant, eventType, member.UserId, member.MembershipGeneration, payload, occurredAt);
    }

    public TenantOutboxMessage StageLifecycleEvent(
        TenantEntity trackedTenant, string eventType, string? fromStatus, string status, DateTime occurredAt)
    {
        ArgumentNullException.ThrowIfNull(trackedTenant);

        if (!LifecycleEventTypes.Contains(eventType))
            throw new ArgumentException($"'{eventType}' is not a lifecycle event type.", nameof(eventType));

        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("A status is required.", nameof(status));

        RequireTracked(trackedTenant);

        var payload = new TenantLifecycleEventPayload(trackedTenant.Id, fromStatus, status, occurredAt);

        return Enqueue(trackedTenant, eventType, null, null, payload, occurredAt);
    }

    private void RequireTracked(TenantEntity tenant)
    {
        // Deleted is still tracked: a tenant being deleted in this same SaveChanges must be able to emit its
        // final event. Only a detached instance would leave the sequence unprotected.
        if (_db.Entry(tenant).State == EntityState.Detached)
            throw new InvalidOperationException(
                "The tenant must be tracked by the same DbContext, otherwise the event sequence is not protected.");
    }

    private TenantOutboxMessage Enqueue(
        TenantEntity tenant, string eventType, Guid? subjectUserId, long? membershipGeneration,
        object payload, DateTime occurredAt)
    {
        // The sequence and the concurrency stamp are bumped together with the change, in the same transaction.
        // A deleted tenant never updates its row again, so the bump is only used for the message's sequence.
        tenant.LastEventSequence++;
        tenant.ConcurrencyStamp = Guid.NewGuid();

        var message = new TenantOutboxMessage
        {
            EventId = Guid.NewGuid(),
            TenantId = tenant.Id,
            Sequence = tenant.LastEventSequence,
            EventType = eventType,
            SchemaVersion = TenantOutboxEventTypes.CurrentSchemaVersion,
            SubjectUserId = subjectUserId,
            MembershipGeneration = membershipGeneration,
            PayloadJson = JsonSerializer.Serialize(payload, payload.GetType()),
            OccurredAt = occurredAt,
            CreatedAt = occurredAt,
            Status = TenantOutboxStatus.Pending,
            NextAttemptAt = occurredAt
        };

        _db.OutboxMessages.Add(message);
        return message;
    }
}