using CoreKit.Access.Abstractions;

namespace CoreKit.Tenant.Integration;

/// <summary>
/// Exposes Tenant's current request scope to Access through the public <see cref="IAccessScopeContext"/> contract.
/// When a tenant is resolved, the scope is <c>AccessScope.ForTenant(tenantId)</c>; otherwise null,
/// so Access scoped calls are refused outside a tenant.
/// </summary>
public sealed class TenantAccessScopeContext : IAccessScopeContext
{
    private readonly ICurrentTenant _currentTenant;

    public TenantAccessScopeContext(ICurrentTenant currentTenant) => _currentTenant = currentTenant;

    public AccessScope? Current => _currentTenant.IsResolved
        ? AccessScope.ForTenant(_currentTenant.Id!.Value)
        : null;
}