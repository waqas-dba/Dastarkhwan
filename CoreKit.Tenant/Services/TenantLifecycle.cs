namespace CoreKit.Tenant.Services;

/// <summary>The only place that knows which status changes are allowed.</summary>
public static class TenantLifecycle
{
    public static bool CanTransition(TenantStatus from, TenantStatus to) => (from, to) switch
    {
        (TenantStatus.Pending, TenantStatus.Active) => true,
        (TenantStatus.Pending, TenantStatus.Archived) => true,
        (TenantStatus.Active, TenantStatus.Suspended) => true,
        (TenantStatus.Active, TenantStatus.Archived) => true,
        (TenantStatus.Suspended, TenantStatus.Active) => true,
        (TenantStatus.Suspended, TenantStatus.Archived) => true,
        (TenantStatus.Archived, TenantStatus.Suspended) => true,
        _ => false
    };
}