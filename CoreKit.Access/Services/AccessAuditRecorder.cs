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
    private readonly IAccessBackgroundContext? _background;

    public AccessAuditRecorder(
        IAccessAuditRepository audit, IAccessCurrentUser actor, TimeProvider time,
        IAccessBackgroundContext? background = null)
    {
        _audit = audit;
        _actor = actor;
        _time = time;
        _background = background;
    }

    public void Record(AccessScope scope, string action, string? details = null)
    {
        var (actorType, actorUserId, actorSource) = ResolveActor();
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

    private (string ActorType, Guid? ActorUserId, string? ActorSource) ResolveActor()
    {
        if (_background?.Current is { ActorKind: AccessBackgroundActorKind.User, ActorUserId: { } delegator } exec
            && delegator != Guid.Empty)
        {
            var source = $"job:{exec.JobKind}" + (exec.GrantId is { } grant ? $";grant:{grant:N}" : string.Empty);
            return (AccessAuditEntry.ActorTypeDelegated, delegator, source.Length > 200 ? source[..200] : source);
        }

        if (_actor.UserId is { } id && id != Guid.Empty && _actor.IsAuthenticated)
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
