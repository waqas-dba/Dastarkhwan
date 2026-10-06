
using Microsoft.AspNetCore.Authorization;

namespace CoreKit.IAM.Authorization;



/// <summary>For controllers in any module: [RequirePermission("users.read")].</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) : base(IamPolicies.ForPermission(permission))
    {
        Permission = permission;
    }

    public string Permission { get; }
}