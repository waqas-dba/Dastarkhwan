using System;
using System.Diagnostics.CodeAnalysis;

namespace CoreKit.SharedKernel;

/// <summary>
/// The policy-name contract shared by every module that protects an endpoint with a permission.
/// The host that registers Identity's authorization services understands these names; a module that
/// only needs to protect its own endpoints can use this helper without referencing Identity.
/// </summary>
public static class PermissionPolicy
{
    // Kept for backwards compatibility with policies already used by the Identity module.
    private const string Prefix = "identity:permission:";

    /// <summary>Returns the authorization policy name for a permission, for example "identity:permission:users.read".</summary>
    public static string NameFor(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return Prefix + permission.Trim().ToLowerInvariant();
    }

    /// <summary>Recognises a policy created by <see cref="NameFor"/> and returns its permission name.</summary>
    public static bool TryGetPermission(string policyName, [NotNullWhen(true)] out string? permission)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            permission = policyName[Prefix.Length..];
            return permission.Length > 0;
        }

        permission = null;
        return false;
    }
}
