namespace CoreKit.Access.Services;

/// <summary>
/// Used only when no identity module registered the kernel's ICurrentUser: the signed-in user's id is read
/// from a claim (IAM issues "sub"), so Access works with any JWT authentication.
/// </summary>
internal sealed class ClaimsCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly string _claimType;

    public ClaimsCurrentUser(IHttpContextAccessor accessor, IOptions<AccessOptions> options)
    {
        _accessor = accessor;
        _claimType = options.Value.UserIdClaimType;
    }

    public bool IsAuthenticated => UserId is not null;

    public Guid? UserId
    {
        get
        {
            var user = _accessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
                return null;

            return Guid.TryParse(user.FindFirst(_claimType)?.Value, out var id) && id != Guid.Empty ? id : null;
        }
    }
}

/// <summary>
/// The scope of a request is its tenant, read through the shared kernel. Without a tenant module
/// there is no scope, so scoped calls are refused.
/// </summary>
internal sealed class TenantAccessScopeContext : IAccessScopeContext
{
    private readonly ICurrentTenant? _tenant;

    public TenantAccessScopeContext(ICurrentTenant? tenant = null) => _tenant = tenant;

    public AccessScope? Current => _tenant?.Id is { } id ? AccessScope.ForTenant(id) : null;
}

/// <summary>
/// Members of a tenant are the people who can hold its roles, read through the shared kernel.
/// Without a tenant module nobody belongs anywhere, so nobody is granted anything.
/// </summary>
internal sealed class TenantAccessMembership : IAccessMembership
{
    private readonly ITenantMembershipReader? _reader;

    public TenantAccessMembership(ITenantMembershipReader? reader = null) => _reader = reader;

    public async Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
    {
        // Other scope types (for example a store) are served by their own membership.
        if (_reader is null || scope.Type != AccessScope.TenantType)
            return null;

        var member = await _reader.FindAsync(scope.Id, userId, ct);

        return member is null ? null : new AccessMember(member.IsOwner);
    }
}