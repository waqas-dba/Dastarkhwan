namespace CoreKit.Access.Services;

/// <summary>Stages an audit entry. It is saved together with the change it describes.</summary>
public interface IAccessAuditRecorder
{
    void Record(AccessScope scope, string action, string? details = null);

    /// <summary>Records a trusted system or deployment operation. The actor is System,
    /// ActorUserId is null, and <paramref name="source"/> identifies the trusted origin
    /// (for example a deployment bootstrap command). Never secrets or credentials.</summary>
    void RecordSystem(AccessScope scope, string action, string source, string? details = null);
}

public sealed class AccessAuditRecorder : IAccessAuditRecorder
{
    private const int MaxDetailsLength = 2000;

    private readonly IAccessAuditRepository _audit;
    private readonly IAccessCurrentUser _actor;
    private readonly TimeProvider _time;

    public AccessAuditRecorder(IAccessAuditRepository audit, IAccessCurrentUser actor, TimeProvider time)
    {
        _audit = audit;
        _actor = actor;
        _time = time;
    }

    public void Record(AccessScope scope, string action, string? details = null)
    {
        var (actorType, actorUserId, actorSource) = ResolveActor(_actor);
        AccessAuditEntry.ValidateActor(actorType, actorUserId, actorSource);
        _audit.Add(new AccessAuditEntry
        {
            Id = Guid.NewGuid(),
            ScopeType = scope.Type,
            ScopeId = scope.Id,
            Action = action,
            ActorUserId = actorUserId,
            ActorType = actorType,
            ActorSource = actorSource,
            Details = Clean(details),
            OccurredAt = _time.GetUtcNow().UtcDateTime
        });
    }

    public void RecordSystem(AccessScope scope, string action, string source, string? details = null)
    {
        AccessAuditEntry.ValidateActor(AccessAuditEntry.ActorTypeSystem, null, source);
        _audit.Add(new AccessAuditEntry
        {
            Id = Guid.NewGuid(),
            ScopeType = scope.Type,
            ScopeId = scope.Id,
            Action = action,
            ActorUserId = null,
            ActorType = AccessAuditEntry.ActorTypeSystem,
            ActorSource = source,
            Details = Clean(details),
            OccurredAt = _time.GetUtcNow().UtcDateTime
        });
    }

    private static (string ActorType, Guid? ActorUserId, string? ActorSource) ResolveActor(IAccessCurrentUser actor)
    {
        if (actor.UserId is { } id && id != Guid.Empty && actor.IsAuthenticated)
            return (AccessAuditEntry.ActorTypeUser, id, null);
        return (AccessAuditEntry.ActorTypeUnknown, null, null);
    }

    /// <summary>
    /// Details can contain names typed by users. Line breaks and other control characters are replaced, so one
    /// entry can never look like several, and the text is cut to the column length.
    /// </summary>
    private static string? Clean(string? details)
    {
        if (details is null)
            return null;

        var cleaned = new string(details.Select(c => char.IsControl(c) ? ' ' : c).ToArray());

        return cleaned.Length > MaxDetailsLength ? cleaned[..MaxDetailsLength] : cleaned;
    }
}
