
using Microsoft.AspNetCore.Authorization;

namespace CoreKit.Identity.Authorization;



/// <summary>For controllers in any module: [RequirePermission("users.read")].</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) : base(IdentityPolicies.ForPermission(permission))
    {
        Permission = permission;
    }

    public string Permission { get; }
}