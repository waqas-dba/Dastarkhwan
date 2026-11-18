using CoreKit.Access.Abstractions;
using CoreKit.Access.Models;
using Microsoft.AspNetCore.Routing;

namespace CoreKit.Access.Endpoints;

public static class AccessEndpointExtensions
{
    /// <summary>
    /// Maps the Access HTTP API under a prefix (default "/access"). Every route needs a signed-in user,
    /// and the services check the permission, so the same rules apply when you call them from code.
    /// </summary>
    public static IEndpointRouteBuilder MapAccess(this IEndpointRouteBuilder app, string prefix = "/access")
    {
        var basePath = EndpointRoutes.NormalizePrefix(prefix);
        var access = app.MapGroup(basePath).WithTags("Access").RequireAuthorization();

        // What the signed-in user may do right now in this scope.
        access.MapGet("/me", async (IAccessService s, CancellationToken ct) =>
            (await s.GetMyAccessAsync(ct)).ToHttpResult());

        // The permissions the application declared, to build a role from.
        access.MapGet("/permissions", async (IAccessRoleService s, CancellationToken ct) =>
            (await s.ListPermissionsAsync(ct)).ToHttpResult());

        access.MapGet("/roles", async (IAccessRoleService s, CancellationToken ct) =>
            (await s.ListAsync(ct)).ToHttpResult());

        access.MapGet("/roles/{id:guid}", async (Guid id, IAccessRoleService s, CancellationToken ct) =>
            (await s.GetAsync(id, ct)).ToHttpResult());

        access.MapPost("/roles", async (CreateAccessRoleRequest r, IAccessRoleService s, CancellationToken ct) =>
        {
            var result = await s.CreateAsync(r, ct);

            return result.IsSuccess
                ? Results.Created($"{basePath}/roles/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        });

        access.MapPut("/roles/{id:guid}", async (Guid id, UpdateAccessRoleRequest r, IAccessRoleService s, CancellationToken ct) =>
            (await s.UpdateAsync(id, r, ct)).ToHttpResult());

        access.MapPut("/roles/{id:guid}/permissions", async (
                Guid id, SetAccessRolePermissionsRequest r, IAccessRoleService s, CancellationToken ct) =>
            (await s.SetPermissionsAsync(id, r, ct)).ToHttpResult());

        access.MapDelete("/roles/{id:guid}", async (Guid id, IAccessRoleService s, CancellationToken ct) =>
            (await s.DeleteAsync(id, ct)).ToHttpResult());

        access.MapGet("/users/{userId:guid}/roles", async (Guid userId, IAccessAssignmentService s, CancellationToken ct) =>
            (await s.GetRolesAsync(userId, ct)).ToHttpResult());

        access.MapPut("/users/{userId:guid}/roles", async (
                Guid userId, SetAccessUserRolesRequest r, IAccessAssignmentService s, CancellationToken ct) =>
            (await s.SetRolesAsync(userId, r, ct)).ToHttpResult());

        access.MapGet("/audit", async (int? page, int? pageSize, IAccessAuditLog s, CancellationToken ct) =>
            (await s.ListAsync(page ?? 1, pageSize ?? 50, ct)).ToHttpResult());

        // Platform membership management (fixed platform scope, never ambient tenant scope)
        var platform = access.MapGroup("/platform/members").WithTags("Access.Platform.Members");

        platform.MapPost("", async (AddPlatformMemberRequest req, IPlatformMembershipService s, CancellationToken ct) =>
        {
            var result = await s.AddMemberAsync(req.UserId, ct);
            return result.IsSuccess
                ? Results.Created($"{basePath}/platform/members/{req.UserId}", result)
                : result.ToHttpResult();
        });

        platform.MapGet("/{userId:guid}", async (Guid userId, IPlatformMembershipService s, CancellationToken ct) =>
        {
            var isMember = await s.IsMemberAsync(userId, ct);
            return isMember ? Results.Ok(new { userId, isMember = true }) : Results.NotFound(new { userId, isMember = false });
        });

        platform.MapDelete("/{userId:guid}", async (Guid userId, IPlatformMembershipService s, CancellationToken ct) =>
        {
            var result = await s.RevokeMemberAsync(userId, ct);
            return result.IsSuccess ? Results.Ok(result) : result.ToHttpResult();
        });

        return app;
    }
}