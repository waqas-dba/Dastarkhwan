using System;
using System.Collections.Generic;
using System.Linq;

namespace CoreKit.Access.Contracts;

/// <summary>
/// Permissions that only make sense at platform scope. A tenant role (or a tenant owner's
/// implicit "everything") must never carry them.
/// </summary>
public static class AccessScopePolicy
{
    private static readonly HashSet<string> Exact = new(StringComparer.Ordinal)
    {
        "tenants.platform", "tenants.create", "tenants.manage_status", "tenants.delete",
        "platform.members.manage"
    };

    private static readonly string[] Prefixes = { "platform.", "identity.users." };

    public static bool IsPlatformOnly(string permission)
        => permission is not null
           && (Exact.Contains(permission) || Prefixes.Any(p => permission.StartsWith(p, StringComparison.Ordinal)));
}
