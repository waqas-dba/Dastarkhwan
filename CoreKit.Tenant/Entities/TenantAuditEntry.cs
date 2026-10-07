namespace CoreKit.Tenant.Entities;

/// <summary>
/// One recorded action. There is deliberately no foreign key to the tenant:
/// the history survives when a tenant is permanently deleted.
/// </summary>
public sealed class TenantAuditEntry
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Action { get; set; } = null!;

    public Guid? ActorUserId { get; set; }

    public string? Details { get; set; }

    public DateTime OccurredAt { get; set; }
}