
using System.Diagnostics.CodeAnalysis;

namespace CoreKit.Identity.Authorization;



public static class IdentityPolicies
{
    private const string PermissionPrefix = "identity:permission:";

    /// <summary>The policy name for a permission, for example "identity:permission:users.read".</summary>
    public static string ForPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return PermissionPrefix + permission.Trim().ToLowerInvariant();
    }

    public static bool TryGetPermission(string policyName, [NotNullWhen(true)] out string? permission)
    {
        if (policyName.StartsWith(PermissionPrefix, StringComparison.Ordinal))
        {
            permission = policyName[PermissionPrefix.Length..];
            return permission.Length > 0;
        }

        permission = null;
        return false;
    }
}