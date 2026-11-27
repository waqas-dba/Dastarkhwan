using CoreKit.Access.Contracts;
using CoreKit.Tenant.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dastarkhwan.Integration;

/// <summary>
/// Safety net for default role provisioning. A new tenant normally gets its roles from the in-process
/// <c>TenantCreatedToAccessProvision</c> handler, which runs after the tenant is saved and cannot undo it if
/// it fails. This startup pass provisions any tenant that still has no roles.
/// </summary>
public static class TenantRoleReconciliation
{
    private const int PageSize = 100;

    /// <summary>
    /// Provisions default roles for every tenant that has none. Provisioning is idempotent (a scope that
    /// already has roles is skipped), so running this on every start is safe. One failing tenant is logged and
    /// does not stop the others. The cost is one lookup per tenant, which suits the MVP; revisit it when the
    /// tenant count makes startup slow.
    /// </summary>
    public static async Task ReconcileTenantRolesAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var tenants = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var maintenance = scope.ServiceProvider.GetRequiredService<IAccessMaintenance>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Dastarkhwan.Integration.TenantRoleReconciliation");

        var checkedCount = 0;
        var failedCount = 0;
        var page = 1;

        while (true)
        {
            var (items, total) = await tenants.ListAsync(search: null, status: null, page, PageSize, ct);

            foreach (var tenant in items)
            {
                try
                {
                    await maintenance.ProvisionScopeAsync(AccessScope.ForTenant(tenant.Id), ct);
                    checkedCount++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failedCount++;
                    logger.LogError(ex, "Provisioning default roles failed for tenant {TenantId}.", tenant.Id);
                }
            }

            if (items.Count == 0 || (long)page * PageSize >= total)
                break;

            page++;
        }

        logger.LogInformation(
            "Tenant role reconciliation finished: {Checked} tenant(s) checked, {Failed} failed.", checkedCount, failedCount);
    }
}