namespace CoreKit.Access.Services;

/// <summary>
/// Used only when no identity module registered Access's IAccessCurrentUser: the signed-in user's id is read
/// from a claim (Identity issues "sub"), so Access works with any JWT authentication.
/// </summary>
internal sealed class ClaimsCurrentUser : IAccessCurrentUser
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