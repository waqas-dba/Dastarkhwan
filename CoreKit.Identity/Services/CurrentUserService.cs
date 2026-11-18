using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;

namespace CoreKit.Identity.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirst("sub")?.Value
                        ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email
        => Principal?.FindFirst("email")?.Value ?? Principal?.FindFirst(ClaimTypes.Email)?.Value;
}
