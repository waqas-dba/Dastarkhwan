using System.Net;
using System.Text.Json;

namespace CoreKit.Tenant.Resolution;

/// <summary>
/// Finds the tenant of each request. If a tenant is identified it must exist and be Active, and every
/// strategy that found one must agree. Requests that identify no tenant continue without one
/// (use RequireTenant() on routes that need a tenant).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

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
}