using CoreKit.Access.Abstractions;
using CoreKit.Access.Entities;
using CoreKit.Access.Persistence;
using CoreKit.Tenant.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.Access.Services;

/// <summary>
/// Unified membership lookup across platform and tenant scopes.
/// Platform scope reads from Access's PlatformMembers table.
/// Tenant scope delegates to Tenant's ITenantMembershipReader.
/// </summary>
internal sealed class AccessMembership : IAccessMembership
{
    private readonly AccessDbContext _db;
    private readonly ITenantMembershipReader _tenantMembershipReader;

    public AccessMembership(AccessDbContext db, ITenantMembershipReader tenantMembershipReader)
    {
        _db = db;
        _tenantMembershipReader = tenantMembershipReader;
    }

    public async Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        if (!scope.IsValid || userId == Guid.Empty)
            return null;

        return scope.Type switch
        {
            AccessScope.PlatformType => await FindPlatformMemberAsync(scope.Id, userId, ct),
            AccessScope.TenantType => await FindTenantMemberAsync(scope.Id, userId, ct),
            _ => null
        };
    }

    private async Task<AccessMember?> FindPlatformMemberAsync(Guid platformId, Guid userId, CancellationToken ct)
    {
        var member = await _db.PlatformMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.PlatformId == platformId && m.RevokedAt == null, ct);

        if (member is null)
            return null;

        // Platform members don't have an "owner" concept; ownership is a tenant concept.
        // Platform administration is granted via the "Administrator" role.
        return new AccessMember(false, member.MembershipGeneration);
    }

    private async Task<AccessMember?> FindTenantMemberAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var info = await _tenantMembershipReader.FindAsync(tenantId, userId, ct);
        if (info is null)
            return null;

        return new AccessMember(info.IsOwner, info.MembershipGeneration);
    }
}