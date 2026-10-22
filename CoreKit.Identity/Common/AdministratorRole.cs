using CoreKit.Identity.Constants;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Normalization;

namespace CoreKit.Identity.Common;

/// <summary>The one place that knows which role is the protected Administrator role.</summary>
public static class AdministratorRole
{
    public static readonly string NormalizedName = IdentityNormalizer.NormalizeName(IdentityRoleNames.Administrator);

    public static bool Is(Role role) => role.NormalizedName == NormalizedName;
}