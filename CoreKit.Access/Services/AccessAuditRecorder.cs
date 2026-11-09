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
    private readonly ICurrentUser _actor;
    private readonly TimeProvider _time;

    public AccessAuditRecorder(IAccessAuditRepository audit, ICurrentUser actor, TimeProvider time)
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
            Details = Clean(details),
            OccurredAt = _time.GetUtcNow().UtcDateTime
        });

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
