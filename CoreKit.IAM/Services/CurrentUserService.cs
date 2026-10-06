using CoreKit.IAM.Constants;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace CoreKit.IAM.Services;

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

    public IReadOnlyCollection<string> Roles => Values(IamClaimTypes.Role);

    public IReadOnlyCollection<string> Permissions => Values(IamClaimTypes.Permission);

    private IReadOnlyCollection<string> Values(string claimType)
        => Principal is null
            ? Array.Empty<string>()
            : Principal.FindAll(claimType).Select(c => c.Value).Distinct().ToList();
}
