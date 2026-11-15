using CoreKit.Access.Abstractions;
using Microsoft.AspNetCore.Http;

namespace CoreKit.Access.Services;

public sealed class ClaimsAccessScopeContext : IAccessScopeContext
{
    private readonly IHttpContextAccessor _accessor;

    public ClaimsAccessScopeContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public AccessScope? Current
    {
        get
        {
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
