namespace CoreKit.Tenant.Services;

/// <summary>
/// Answers "who belongs to this tenant" for other modules through the shared kernel.
/// It applies no caller checks of its own: it is server-side code and is never exposed over HTTP.
/// </summary>
public sealed class TenantMembershipReader : ITenantMembershipReader
{
    private readonly ITenantMemberRepository _members;

    public TenantMembershipReader(ITenantMemberRepository members) => _members = members;

    public async Task<TenantMemberInfo?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
            return null;

        var member = await _members.FindAsync(tenantId, userId, ct);

        return member is null ? null : new TenantMemberInfo(member.IsOwner);
    }
}