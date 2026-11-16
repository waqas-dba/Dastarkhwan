namespace CoreKit.Access.Abstractions;

/// <summary>
/// Routes membership lookups to the right adapter. When an authoritative projection adapter
/// is present and answers for a scope, its result is final: a found member is returned, and a
/// missing, stale, quarantined, inactive, unknown or failed projection denies without falling
/// through to another source. Only when no authoritative adapter answers does the composite
/// consult the legacy first-match adapters (platform membership, tenant adapter, default).
/// Registered as the single <see cref="IAccessMembership"/> so there are no competing
/// last-wins registrations. Depends on <see cref="IEnumerable{IAccessMembershipAdapter}"/> so it
/// never sees itself and there is no circular DI dependency.
/// </summary>
internal sealed class CompositeAccessMembership : IAccessMembership
{
    private readonly IEnumerable<IAccessMembershipAdapter> _adapters;

    public CompositeAccessMembership(IEnumerable<IAccessMembershipAdapter> adapters)
    {
        _adapters = adapters.ToList();
    }

    public async Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        foreach (var adapter in _adapters)
        {
            // Authoritative adapters decide for themselves whether they answer for this scope.
            // When they do, their result is final and the composite must not fall through.
            if (adapter is IProjectionMembershipAdapter authoritative)
            {
                var result = await authoritative.TryFindAsync(scope, userId, ct);

                if (result.IsAuthoritative)
                    return result.IsFound ? result.Member : null;

                continue;
            }

            var member = await adapter.FindAsync(scope, userId, ct);
            if (member is not null)
                return member;
        }

        return null;
    }
}