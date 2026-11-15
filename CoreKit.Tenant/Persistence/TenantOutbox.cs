using System.Text.Json;
using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Events;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.Tenant.Persistence;

/// <summary>
/// Stages an outbox message. It is saved by the same <c>SaveChanges</c> as the membership change,
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

        if (_db.Entry(trackedTenant).State == EntityState.Detached)
            throw new InvalidOperationException(
                "The tenant must be tracked by the same DbContext, otherwise the event sequence is not protected.");

        if (member.MembershipGeneration < 1)
            throw new InvalidOperationException("A membership generation must be at least 1.");

        var isActive = member.RemovedAt is null;
        if ((eventType == TenantOutboxEventTypes.MemberRemoved) == isActive)
            throw new InvalidOperationException(
                "Only a removed member may produce a removal event, and a removal event requires a removed member.");

        trackedTenant.LastEventSequence++;
        trackedTenant.ConcurrencyStamp = Guid.NewGuid();

        var payload = new TenantMembershipEventPayload(
            trackedTenant.Id, member.UserId, member.MembershipGeneration, member.IsOwner, isActive, occurredAt);

        var message = new TenantOutboxMessage
        {
            EventId = Guid.NewGuid(),
            TenantId = trackedTenant.Id,
            Sequence = trackedTenant.LastEventSequence,
            EventType = eventType,
            SchemaVersion = TenantOutboxEventTypes.CurrentSchemaVersion,
            SubjectUserId = member.UserId,
            MembershipGeneration = member.MembershipGeneration,
            PayloadJson = JsonSerializer.Serialize(payload),
            OccurredAt = occurredAt,
            CreatedAt = occurredAt,
            Status = TenantOutboxStatus.Pending,
            NextAttemptAt = occurredAt
        };

        _db.OutboxMessages.Add(message);
        return message;
    }
}