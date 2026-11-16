namespace CoreKit.Access.Persistence;

/// <summary>
/// Access-owned persistence for projection trust: the Tenant heartbeat watermark and the per-tenant
/// quarantine list. A missing or stale watermark, or a quarantined tenant, makes projection lookups deny.
/// </summary>
internal interface IAccessMembershipFreshnessRepository
{
    /// <summary>Read-only. Null until Tenant has sent at least one heartbeat.</summary>
    Task<AccessMembershipWatermark?> GetWatermarkAsync(CancellationToken ct = default);

    Task<bool> IsQuarantinedAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Tracked, so the recorder can remove it.</summary>
    Task<AccessQuarantinedTenant?> FindQuarantineAsync(Guid tenantId, CancellationToken ct = default);

    void AddQuarantine(AccessQuarantinedTenant quarantine);

    void RemoveQuarantine(AccessQuarantinedTenant quarantine);

    /// <summary>Tracked, so the recorder can advance it without moving it backwards.</summary>
    Task<AccessMembershipWatermark?> FindWatermarkAsync(CancellationToken ct = default);

    void AddWatermark(AccessMembershipWatermark watermark);
}

internal sealed class AccessMembershipFreshnessRepository : IAccessMembershipFreshnessRepository
{
    private readonly AccessDbContext _db;

    public AccessMembershipFreshnessRepository(AccessDbContext db) => _db = db;

    public Task<AccessMembershipWatermark?> GetWatermarkAsync(CancellationToken ct = default)
        => _db.MembershipWatermarks.AsNoTracking().FirstOrDefaultAsync(w => w.Id == Guid.Empty, ct);

    public Task<AccessMembershipWatermark?> FindWatermarkAsync(CancellationToken ct = default)
        => _db.MembershipWatermarks.FirstOrDefaultAsync(w => w.Id == Guid.Empty, ct);

    public void AddWatermark(AccessMembershipWatermark watermark) => _db.MembershipWatermarks.Add(watermark);

    public Task<bool> IsQuarantinedAsync(Guid tenantId, CancellationToken ct = default)
        => _db.QuarantinedTenants.AnyAsync(q => q.TenantId == tenantId, ct);

    public Task<AccessQuarantinedTenant?> FindQuarantineAsync(Guid tenantId, CancellationToken ct = default)
        => _db.QuarantinedTenants.FirstOrDefaultAsync(q => q.TenantId == tenantId, ct);

    public void AddQuarantine(AccessQuarantinedTenant quarantine) => _db.QuarantinedTenants.Add(quarantine);

    public void RemoveQuarantine(AccessQuarantinedTenant quarantine) => _db.QuarantinedTenants.Remove(quarantine);
}