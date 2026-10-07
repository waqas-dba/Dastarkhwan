namespace CoreKit.Tenant.Services;

internal static class TenantMapper
{
    public static TenantDto ToDto(TenantEntity tenant) => new()
    {
        Id = tenant.Id,
        Name = tenant.Name,
        Slug = tenant.Slug,
        Status = tenant.Status,
        StatusReason = tenant.StatusReason,
        CreatedAt = tenant.CreatedAt,
        UpdatedAt = tenant.UpdatedAt,
        StatusChangedAt = tenant.StatusChangedAt
    };

    public static TenantInfo ToInfo(TenantEntity tenant)
        => new(tenant.Id, tenant.Slug, tenant.Name, tenant.Status);

    public static TenantSettingDto ToDto(TenantSetting setting) => new()
    {
        Key = setting.Key,
        Value = setting.Value,
        UpdatedAt = setting.UpdatedAt
    };

    public static TenantMemberDto ToDto(TenantMember member) => new()
    {
        TenantId = member.TenantId,
        UserId = member.UserId,
        IsOwner = member.IsOwner,
        IsDefault = member.IsDefault,
        JoinedAt = member.JoinedAt
    };

    /// <summary>The member must be loaded with its Tenant.</summary>
    public static TenantMembershipDto ToMembershipDto(TenantMember member) => new()
    {
        TenantId = member.TenantId,
        Slug = member.Tenant.Slug,
        Name = member.Tenant.Name,
        Status = member.Tenant.Status,
        IsOwner = member.IsOwner,
        IsDefault = member.IsDefault
    };

    public static TenantAuditEntryDto ToDto(TenantAuditEntry entry) => new()
    {
        Id = entry.Id,
        TenantId = entry.TenantId,
        Action = entry.Action,
        ActorUserId = entry.ActorUserId,
        Details = entry.Details,
        OccurredAt = entry.OccurredAt
    };
}