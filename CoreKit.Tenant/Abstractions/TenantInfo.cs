namespace CoreKit.Tenant.Abstractions;

/// <summary>The small, cacheable view of a tenant that request handling needs.</summary>
public sealed record TenantInfo(Guid Id, string Slug, string Name, TenantStatus Status);