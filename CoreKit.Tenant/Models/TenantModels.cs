using System.Text.Json.Serialization;

namespace CoreKit.Tenant.Models;

public sealed class TenantDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TenantStatus Status { get; set; }

    public string? StatusReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime StatusChangedAt { get; set; }
}

public sealed class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional. Generated from the name when empty.</summary>
    public string? Slug { get; set; }

    /// <summary>False creates the tenant as Pending, for flows that need approval first.</summary>
    public bool ActivateImmediately { get; set; } = true;

    /// <summary>Optional. This user becomes the first owner and member.</summary>
    public Guid? OwnerUserId { get; set; }
}

/// <summary>Every property is optional. A property that is not sent stays unchanged.</summary>
public sealed class UpdateTenantRequest
{
    public string? Name { get; set; }

    public string? Slug { get; set; }
}

public sealed class ChangeTenantStatusRequest
{
    public string? Reason { get; set; }
}

public sealed class TenantListQuery
{
    public string? Search { get; set; }

    public TenantStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed class TenantPagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class TenantSettingDto
{
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }
}

public sealed class SetTenantSettingRequest
{
    public string Value { get; set; } = string.Empty;
}

public sealed class SetTenantSettingsRequest
{
    public Dictionary<string, string> Values { get; set; } = new();
}

public sealed class TenantMemberDto
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public bool IsOwner { get; set; }

    public bool IsDefault { get; set; }

    public DateTime JoinedAt { get; set; }
}

public sealed class TenantMembershipDto
{
    public Guid TenantId { get; set; }

    public string Slug { get; set; } = null!;

    public string Name { get; set; } = null!;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TenantStatus Status { get; set; }

    public bool IsOwner { get; set; }

    public bool IsDefault { get; set; }
}

public sealed class AddTenantMemberRequest
{
    public Guid UserId { get; set; }

    public bool IsOwner { get; set; }
}

public sealed class SetTenantOwnerRequest
{
    public bool IsOwner { get; set; }
}

public sealed class TenantAuditEntryDto
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Action { get; set; } = null!;

    public Guid? ActorUserId { get; set; }

    public string? Details { get; set; }

    public DateTime OccurredAt { get; set; }
}

public sealed class TenantAuditQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}