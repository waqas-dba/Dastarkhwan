using CoreKit.Access.Abstractions;
using CoreKit.Access.Contracts;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace CoreKit.Access.Services;

public sealed class ClaimsAccessScopeContext : IAccessScopeContext
{
    private readonly IHttpContextAccessor _accessor;

    public ClaimsAccessScopeContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public AccessScope? Current
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            // Check for platform admin claim (added by PlatformAdminClaimsContributor during token issuance).
            // Platform administrators have platform scope regardless of tenant membership.
            if (user.HasClaim("platform_admin", "true"))
            {
                return AccessScope.ForPlatform(AccessScope.PlatformId);
            }

            // Fall back to tenant scope from HttpContext.Items (set by TenantResolutionMiddleware).
            var items = _accessor.HttpContext?.Items;
            if (items is null || !items.TryGetValue(AccessScopeContextItemKeys.TenantId, out var value))
                return null;

            return value switch
            {
                Guid id when id != Guid.Empty => AccessScope.ForTenant(id),
                _ => null
            };
        }
    }
}
