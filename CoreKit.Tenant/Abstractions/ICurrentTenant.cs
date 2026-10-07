namespace CoreKit.Tenant.Abstractions;

/// <summary>
/// The tenant of the current request or operation. Application code reads this and never has to
/// know how the tenant was found.
/// </summary>
public interface ICurrentTenant
{
    bool IsResolved { get; }

    Guid? Id { get; }

    /// <summary>The tenant id, or an exception when there is none. Use it where a tenant is mandatory.</summary>
    /// <exception cref="TenantNotResolvedException">No tenant was resolved.</exception>
    Guid RequiredId { get; }

    /// <summary>
    /// Acts as another tenant (or as no tenant, with null) until the returned object is disposed.
    /// For background jobs, seeders and tests.
    /// </summary>
    IDisposable Change(Guid? tenantId);
}