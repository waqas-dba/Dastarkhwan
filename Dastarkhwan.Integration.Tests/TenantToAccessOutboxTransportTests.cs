using CoreKit.Access.Abstractions;
using CoreKit.Tenant.Outbox;
using Dastarkhwan.Integration.Membership;
using Dastarkhwan.Integration.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dastarkhwan.Integration.Tests;

public sealed class TenantToAccessOutboxTransportTests
{
    private static TenantOutboxEnvelope NewEnvelope() => new(
        EventId: Guid.NewGuid(),
        TenantId: Guid.NewGuid(),
        Sequence: 7,
        EventType: AccessMembershipEventTypes.MemberRemoved,
        SchemaVersion: 1,
        SubjectUserId: Guid.NewGuid(),
        MembershipGeneration: 3,
        PayloadJson: "{\"k\":\"v\"}",
        OccurredAt: new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc),
        AttemptCount: 4);

    private static TenantToAccessOutboxTransport Create(FakeMembershipEventConsumer consumer)
        => new(consumer, NullLogger<TenantToAccessOutboxTransport>.Instance);

    [Fact]
    public async Task Applied_event_is_accepted()
    {
        var consumer = new FakeMembershipEventConsumer { Result = AccessMembershipEventResult.Applied() };

        var result = await Create(consumer).DeliverAsync(NewEnvelope(), CancellationToken.None);

        Assert.Equal(TenantOutboxDeliveryOutcome.Accepted, result.Outcome);
    }

    [Fact]
    public async Task Duplicate_event_is_accepted_because_access_already_holds_it()
    {
        var consumer = new FakeMembershipEventConsumer { Result = AccessMembershipEventResult.Duplicate() };

        var result = await Create(consumer).DeliverAsync(NewEnvelope(), CancellationToken.None);

        Assert.Equal(TenantOutboxDeliveryOutcome.Accepted, result.Outcome);
    }

    [Fact]
    public async Task RetryLater_is_not_acknowledged_and_keeps_the_reason()
    {
        var consumer = new FakeMembershipEventConsumer
        {
            Result = AccessMembershipEventResult.RetryLater("sequence gap")
        };

        var result = await Create(consumer).DeliverAsync(NewEnvelope(), CancellationToken.None);

        Assert.Equal(TenantOutboxDeliveryOutcome.RetryLater, result.Outcome);
        Assert.Equal("sequence gap", result.Error);
    }

    [Fact]
    public async Task Rejected_is_preserved_and_never_converted_to_success()
    {
        var consumer = new FakeMembershipEventConsumer
        {
            Result = AccessMembershipEventResult.Rejected("unsupported schema")
        };

        var result = await Create(consumer).DeliverAsync(NewEnvelope(), CancellationToken.None);

        Assert.Equal(TenantOutboxDeliveryOutcome.Rejected, result.Outcome);
        Assert.Equal("unsupported schema", result.Error);
    }

    [Fact]
    public async Task Unrecognised_access_outcome_is_retryable_not_accepted()
    {
        var consumer = new FakeMembershipEventConsumer
        {
            Result = new AccessMembershipEventResult((AccessMembershipEventOutcome)99, "future outcome")
        };

        var result = await Create(consumer).DeliverAsync(NewEnvelope(), CancellationToken.None);

        Assert.Equal(TenantOutboxDeliveryOutcome.RetryLater, result.Outcome);
    }

    [Fact]
    public async Task Event_identity_is_passed_through_unchanged()
    {
        var consumer = new FakeMembershipEventConsumer();
        var envelope = NewEnvelope();

        await Create(consumer).DeliverAsync(envelope, CancellationToken.None);

        var received = Assert.IsType<AccessMembershipEventEnvelope>(consumer.Received);
        Assert.Equal(envelope.EventId, received.EventId);
        Assert.Equal(envelope.TenantId, received.TenantId);
        Assert.Equal(envelope.Sequence, received.Sequence);
        Assert.Equal(envelope.EventType, received.EventType);
        Assert.Equal(envelope.SchemaVersion, received.SchemaVersion);
        Assert.Equal(envelope.SubjectUserId, received.SubjectUserId);
        Assert.Equal(envelope.MembershipGeneration, received.MembershipGeneration);
        Assert.Equal(envelope.PayloadJson, received.PayloadJson);
        Assert.Equal(envelope.OccurredAt, received.OccurredAt);
    }

    [Fact]
    public async Task Consumer_exceptions_are_not_swallowed()
    {
        var consumer = new FakeMembershipEventConsumer { Throw = new InvalidOperationException("db down") };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Create(consumer).DeliverAsync(NewEnvelope(), CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_token_reaches_the_consumer()
    {
        var consumer = new FakeMembershipEventConsumer();
        using var cts = new CancellationTokenSource();

        await Create(consumer).DeliverAsync(NewEnvelope(), cts.Token);

        Assert.Equal(cts.Token, consumer.ReceivedToken);
    }

    [Fact]
    public async Task Null_envelope_is_rejected_loudly()
    {
        var consumer = new FakeMembershipEventConsumer();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Create(consumer).DeliverAsync(null!, CancellationToken.None));
        Assert.Equal(0, consumer.Calls);
    }
}