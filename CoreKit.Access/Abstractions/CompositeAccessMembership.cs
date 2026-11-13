namespace CoreKit.Access.Abstractions;

/// <summary>
/// Routes membership lookups to the right adapter based on scope type. Tenant scope goes to
/// TenantAccessMembership, platform scope goes to PlatformAccessMembership, everything else
/// returns null (fail-closed). Registered as the single IAccessMembership so there are no
/// competing last-wins registrations. Depends on IEnumerable<IAccessMembershipAdapter> so it
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
            var member = await adapter.FindAsync(scope, userId, ct);
            if (member is not null)
                return member;
        }

        return null;
    }
}
