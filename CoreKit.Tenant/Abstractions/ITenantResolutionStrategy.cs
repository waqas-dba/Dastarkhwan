namespace CoreKit.Tenant.Abstractions;

/// <summary>What a strategy found: a tenant id, a slug, or both, and where it came from.</summary>
public sealed record TenantIdentifier(Guid? Id, string? Slug, string Source);

/// <summary>
/// One way of finding the tenant of a request. Register your own (for example a custom-domain lookup in a
/// separate module) with services.AddScoped&lt;ITenantResolutionStrategy, YourStrategy&gt;().
/// If strategies find different tenants the request is rejected, so a client cannot pick a tenant freely.
/// </summary>
public interface ITenantResolutionStrategy
{
    /// <summary>Lower runs first.</summary>
    int Order { get; }

    Task<TenantIdentifier?> ResolveAsync(HttpContext httpContext, CancellationToken ct = default);
}