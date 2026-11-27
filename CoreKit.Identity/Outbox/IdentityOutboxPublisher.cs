using CoreKit.Identity.Entities;
using CoreKit.Identity.Contracts;
using CoreKit.Identity.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CoreKit.Identity.Outbox;

public sealed class IdentityOutboxPublisher : IIdentityEventPublisher
{
    private readonly IdentityDbContext _db;
    private readonly ILogger<IdentityOutboxPublisher> _logger;

    public IdentityOutboxPublisher(IdentityDbContext db, ILogger<IdentityOutboxPublisher> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIdentityIntegrationEvent
    {
        var eventType = typeof(TEvent).Name;
        var payloadJson = JsonSerializer.Serialize(integrationEvent);
        var occurredAt = integrationEvent.OccurredAt;

        var message = new IdentityOutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            SchemaVersion = 1,
            SubjectUserId = GetSubjectUserId(integrationEvent),
            PayloadJson = payloadJson,
            OccurredAt = occurredAt,
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow,
        };

        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug("Identity outbox event {EventId} ({EventType}) staged for user {UserId}",
            message.EventId, eventType, message.SubjectUserId);
    }

    private static Guid? GetSubjectUserId(IIdentityIntegrationEvent evt)
    {
        var prop = evt.GetType().GetProperty("UserId");
        if (prop != null && prop.PropertyType == typeof(Guid))
            return (Guid?)prop.GetValue(evt);
        return null;
    }
}
