namespace CoreKit.Access.Abstractions;

/// <summary>
/// Routes membership lookups to the right adapter. Authoritative projection adapters are consulted
/// first, independent of registration order. When one answers for a scope, its result is final: a
/// found member is returned, and a missing, stale, quarantined, inactive, unknown or failed
/// projection denies without falling through to another source. Only when no authoritative adapter
/// answers does the composite consult the remaining first-match adapters (platform membership,
/// tenant adapter, default). Registered as the single <see cref="IAccessMembership"/> so there are
/// no competing last-wins registrations. Depends on <see cref="IEnumerable{IAccessMembershipAdapter}"/>
/// so it never sees itself and there is no circular DI dependency.
/// </summary>
internal sealed class CompositeAccessMembership : IAccessMembership
{
    private readonly IReadOnlyList<IAccessMembershipAdapter> _adapters;

    public CompositeAccessMembership(IEnumerable<IAccessMembershipAdapter> adapters)
    {
        _adapters = adapters.ToList();
    }

    public async Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        // Pass 1: authoritative adapters, regardless of where they sit in the registration order.
        foreach (var adapter in _adapters)
        {
            if (adapter is not IProjectionMembershipAdapter authoritative)
                continue;

            var result = await authoritative.TryFindAsync(scope, userId, ct);

            if (result.IsAuthoritative)
                return result.IsFound ? result.Member : null;
        }

        // Pass 2: first-match adapters. Authoritative adapters that declined are not asked again.
        foreach (var adapter in _adapters)
        {
            if (adapter is IProjectionMembershipAdapter)
                continue;

            var member = await adapter.FindAsync(scope, userId, ct);
            if (member is not null)
                return member;
        }

        return null;
    }
}