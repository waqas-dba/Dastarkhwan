namespace CoreKit.Access.Services;

/// <summary>
/// The default actor: the signed-in user's id from a claim (IAM issues "sub"). It works with any JWT
/// authentication, so Access does not need to know about IAM.
/// </summary>
internal sealed class ClaimsAccessActor : IAccessActor
{
    private readonly IHttpContextAccessor _accessor;
    private readonly string _claimType;

    public ClaimsAccessActor(IHttpContextAccessor accessor, IOptions<AccessOptions> options)
    {
        _accessor = accessor;
        _claimType = options.Value.UserIdClaimType;
    }

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

/// <summary>Used until a bridge supplies the real scope: there is no scope, so scoped calls are refused.</summary>
internal sealed class NoAccessScopeContext : IAccessScopeContext
{
    public AccessScope? Current => null;
}

/// <summary>Used until a bridge supplies real membership: nobody belongs anywhere, so nobody is granted anything.</summary>
internal sealed class NoAccessMembership : IAccessMembership
{
    public Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
        => Task.FromResult<AccessMember?>(null);
}