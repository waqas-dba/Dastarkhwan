namespace CoreKit.Access.Entities;

/// <summary>
/// Access's copy of a tenant's lifecycle status. Only an Active tenant authorizes through the projection.
/// Pending, Suspended, Archived, Deleted and an unknown state all deny.
/// </summary>
public sealed class AccessTenantState
{
    public Guid TenantId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }
}