namespace CoreKit.Tenant.Services;

/// <summary>Stages an audit entry. It is saved together with the change it describes.</summary>
public interface ITenantAuditRecorder
{
    void Record(Guid tenantId, string action, string? details = null);
}

public sealed class TenantAuditRecorder : ITenantAuditRecorder
{
    private const int MaxDetailsLength = 2000;

    private readonly ITenantAuditRepository _audit;
    private readonly ITenantActor _actor;
    private readonly TimeProvider _time;

    public TenantAuditRecorder(ITenantAuditRepository audit, ITenantActor actor, TimeProvider time)
    {
        _audit = audit;
        _actor = actor;
        _time = time;
    }

    public void Record(Guid tenantId, string action, string? details = null)
        => _audit.Add(new TenantAuditEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Action = action,
            ActorUserId = _actor.UserId,
            Details = details is { Length: > MaxDetailsLength } ? details[..MaxDetailsLength] : details,
            OccurredAt = _time.GetUtcNow().UtcDateTime
        });
}