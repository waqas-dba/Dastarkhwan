namespace CoreKit.Tenant.Entities;

public enum TenantStatus
{
    /// <summary>Created but not activated yet (for example, waiting for approval).</summary>
    Pending = 0,

    Active = 1,

    Suspended = 2,

    /// <summary>Closed. Read-only, and the only status from which a tenant can be deleted.</summary>
    Archived = 3
}