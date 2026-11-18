using CoreKit.Access.Abstractions;
using CoreKit.Tenant.Outbox;
using Microsoft.Extensions.Logging;

namespace Dastarkhwan.Integration.Membership;

/// <summary>
/// Delivers Tenant outbox events to Access's durable membership-event consumer.
///
/// Delivery is at-least-once: the Tenant dispatcher may deliver the same event again after a crash or lease
/// expiry, and Access deduplicates on EventId. This class therefore changes nothing about an event's identity.
/// EventId, TenantId, Sequence, EventType, SchemaVersion, SubjectUserId, MembershipGeneration, PayloadJson and
/// OccurredAt are passed through unchanged.
///
/// Acknowledgement rule: Tenant is told <c>Accepted</c> only when Access reports that it durably holds the event
/// (Applied, or Duplicate of one it already holds). Anything else is never reported as success:
/// - RetryLater (a sequence gap or a temporary failure) stays retryable, so Tenant retries with backoff.
/// - Rejected is preserved as Rejected so Tenant dead-letters it for operational handling.
/// - An outcome this code does not recognise is treated as RetryLater, never as Accepted, so a future Access
///   outcome cannot be silently acknowledged.
///
/// Exceptions are deliberately not caught. The Tenant dispatcher treats an exception as RetryLater, and
/// cancellation must propagate so shutdown is not misreported as a delivery result.
/// </summary>
public sealed class TenantToAccessOutboxTransport : ITenantOutboxTransport
{
    private readonly IAccessMembershipEventConsumer _consumer;
    private readonly ILogger<TenantToAccessOutboxTransport> _logger;

    public TenantToAccessOutboxTransport(
        IAccessMembershipEventConsumer consumer,
        ILogger<TenantToAccessOutboxTransport> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    public async Task<TenantOutboxDeliveryResult> DeliverAsync(TenantOutboxEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var forAccess = new AccessMembershipEventEnvelope(
            envelope.EventId,
            envelope.TenantId,
            envelope.Sequence,
            envelope.EventType,
            envelope.SchemaVersion,
            envelope.SubjectUserId,
            envelope.MembershipGeneration,
            envelope.PayloadJson,
            envelope.OccurredAt);

        var result = await _consumer.ApplyAsync(forAccess, ct);

        switch (result.Outcome)
        {
            case AccessMembershipEventOutcome.Applied:
            case AccessMembershipEventOutcome.Duplicate:
                return TenantOutboxDeliveryResult.Accepted();

            case AccessMembershipEventOutcome.RetryLater:
                // Logged without the payload. A sequence gap shows up here repeatedly until the missing event
                // is delivered or the tenant is recovered from a snapshot.
                _logger.LogWarning(
                    "Access asked to retry tenant event {EventId} (tenant {TenantId}, sequence {Sequence}, type {EventType}, attempt {Attempt}): {Reason}",
                    envelope.EventId, envelope.TenantId, envelope.Sequence, envelope.EventType,
                    envelope.AttemptCount, result.Reason);
                return TenantOutboxDeliveryResult.RetryLater(result.Reason ?? "Access asked to retry the event.");

            case AccessMembershipEventOutcome.Rejected:
                _logger.LogError(
                    "Access permanently rejected tenant event {EventId} (tenant {TenantId}, sequence {Sequence}, type {EventType}): {Reason}",
                    envelope.EventId, envelope.TenantId, envelope.Sequence, envelope.EventType, result.Reason);
                return TenantOutboxDeliveryResult.Rejected(result.Reason ?? "Access rejected the event.");

            default:
                _logger.LogError(
                    "Access returned an unrecognised outcome {Outcome} for tenant event {EventId} (tenant {TenantId}, sequence {Sequence}); treating it as retryable.",
                    (int)result.Outcome, envelope.EventId, envelope.TenantId, envelope.Sequence);
                return TenantOutboxDeliveryResult.RetryLater("Access returned an unrecognised outcome.");
        }
    }
}