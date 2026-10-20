namespace CoreKit.Access.Services;

/// <summary>Stages an audit entry. It is saved together with the change it describes.</summary>
internal interface IAccessAuditRecorder
{
    void Record(AccessScope scope, string action, string? details = null);
}

internal sealed class AccessAuditRecorder : IAccessAuditRecorder
{
    private const int MaxDetailsLength = 2000;

    private readonly IAccessAuditRepository _audit;
    private readonly IAccessActor _actor;
    private readonly TimeProvider _time;

    public AccessAuditRecorder(IAccessAuditRepository audit, IAccessActor actor, TimeProvider time)
    {
        _audit = audit;
        _actor = actor;
        _time = time;
    }

    public void Record(AccessScope scope, string action, string? details = null)
        => _audit.Add(new AccessAuditEntry
        {
            Id = Guid.NewGuid(),
            ScopeType = scope.Type,
            ScopeId = scope.Id,
            Action = action,
            ActorUserId = _actor.UserId,
            Details = details is { Length: > MaxDetailsLength } ? details[..MaxDetailsLength] : details,
            OccurredAt = _time.GetUtcNow().UtcDateTime
        });
}