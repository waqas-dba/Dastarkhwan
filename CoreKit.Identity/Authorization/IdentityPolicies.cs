
using System.Diagnostics.CodeAnalysis;
using CoreKit.SharedKernel;

namespace CoreKit.Identity.Authorization;



public static class IdentityPolicies
{
    /// <summary>The policy name for a permission, for example "identity:permission:users.read".</summary>
    public static string ForPermission(string permission) => PermissionPolicy.NameFor(permission);

    public static bool TryGetPermission(string policyName, [NotNullWhen(true)] out string? permission)
        => PermissionPolicy.TryGetPermission(policyName, out permission);
}