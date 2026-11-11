using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace CoreKit.Tenant.Resolution;

/// <summary>
/// Finds the tenant of each request. If a tenant is identified it must exist and be Active, and every
/// strategy that found one must agree. Requests that identify no tenant continue without one
/// (use RequireTenant() on routes that need a tenant).
///
/// After resolving a tenant, the middleware also validates that the authenticated user is a current
/// member of that tenant. Removed or never-members are refused even when they hold a valid JWT with
/// a tenant claim.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITenantMembershipReader _membershipReader;

    public TenantResolutionMiddleware(RequestDelegate next, ITenantMembershipReader membershipReader)
    {
        _next = next;
        _membershipReader = membershipReader;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IEnumerable<ITenantResolutionStrategy> strategies,
        ITenantInfoProvider provider,
        ITenantContextSetter setter)
    {
        var ct = context.RequestAborted;
        var identifiers = new List<TenantIdentifier>();

        foreach (var strategy in strategies.OrderBy(s => s.Order))
        {
            var identifier = await strategy.ResolveAsync(context, ct);

            if (identifier is not null)
                identifiers.Add(identifier);
        }

        if (identifiers.Count == 0)
        {
            await _next(context);
            return;
        }

        var tenants = new List<TenantInfo>();

        foreach (var identifier in identifiers)
        {
            TenantInfo? info = null;

            if (identifier.Id is { } id)
                info = await provider.GetByIdAsync(id, ct);
            else if (!string.IsNullOrWhiteSpace(identifier.Slug))
                info = await provider.GetBySlugAsync(identifier.Slug, ct);

            if (info is null)
            {
                await WriteErrorAsync(context, TenantErrors.UnknownTenant);
                return;
            }

            tenants.Add(info);
        }

        if (tenants.Select(t => t.Id).Distinct().Count() > 1)
        {
            await WriteErrorAsync(context, TenantErrors.Mismatch);
            return;
        }

        var tenant = tenants[0];

        if (tenant.Status != TenantStatus.Active)
        {
            await WriteErrorAsync(context, TenantErrors.ForStatus(tenant.Status));
            return;
        }

        // Validate current membership for authenticated users. A valid JWT with a tenant claim is
        // not enough — the user must currently be a member of the resolved tenant. Removed members
        // are refused even when their token has not expired. An authenticated request without a
        // valid user ID cannot be mapped to a member and is refused.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = GetUserId(context.User);
            if (userId is null)
            {
                await WriteErrorAsync(context, TenantErrors.Forbidden("Invalid user identity."));
                return;
            }
            var member = await _membershipReader.FindAsync(tenant.Id, userId.Value, ct);
            if (member is null)
            {
                await WriteErrorAsync(context, TenantErrors.Forbidden("You are not a member of this tenant."));
                return;
            }
        }

        setter.Set(tenant.Id);

        await _next(context);
    }

    private static async Task WriteErrorAsync(HttpContext context, TenantError error)
    {
        context.Response.StatusCode = Endpoints.TenantResultExtensions.StatusCodeFor(error.Kind);
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { code = error.Code, message = error.Message }),
            context.RequestAborted);
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
    }
}
