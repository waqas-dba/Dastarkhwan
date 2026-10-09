using CoreKit.IAM.Constants;
using CoreKit.IAM.Entities;
using CoreKit.IAM.Normalization;

namespace CoreKit.IAM.Common;

/// <summary>The one place that knows which role is the protected Administrator role.</summary>
public static class AdministratorRole
{
    public static readonly string NormalizedName = IamNormalizer.NormalizeName(IamRoleNames.Administrator);

    public static bool Is(Role role) => role.NormalizedName == NormalizedName;
}