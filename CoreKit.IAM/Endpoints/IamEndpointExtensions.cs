using Microsoft.AspNetCore.Routing;

namespace CoreKit.IAM.Endpoints;

public static class IamEndpointExtensions
{
    /// <summary>
    /// Maps the IAM HTTP API under a prefix (default "/iam"). Optional: an app that wants
    /// its own routes can skip this and call the services directly.
    /// </summary>
    public static IEndpointRouteBuilder MapIam(this IEndpointRouteBuilder app, string prefix = "/iam")
    {
        prefix = NormalizePrefix(prefix);

        var iam = app.MapGroup(prefix).WithTags("IAM");

        MapAuth(iam.MapGroup("/auth"));
        MapUsers(iam.MapGroup("/users"), $"{prefix}/users");
        MapRoles(iam.MapGroup("/roles"), $"{prefix}/roles");
        MapPermissions(iam.MapGroup("/permissions"));

        return app;
    }

    /// <summary>"iam", "/iam/" and "/iam" all become "/iam". An empty prefix stays empty.</summary>
    private static string NormalizePrefix(string? prefix)
    {
        var trimmed = (prefix ?? string.Empty).Trim().TrimEnd('/');

        return trimmed.Length == 0 || trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }
    private static void MapAuth(RouteGroupBuilder auth)
    {
        auth.MapGet("/me", async (IAuthService s, CancellationToken ct) =>
                (await s.GetCurrentUserAsync(ct)).ToHttpResult())
            .RequireAuthorization();

        var limited = auth.MapGroup("").RequireRateLimiting(IamRateLimiting.AuthPolicy);

        limited.MapPost("/login", async (LoginRequest r, IAuthService s, CancellationToken ct) =>
                (await s.LoginAsync(r, ct)).ToHttpResult())
            .AllowAnonymous();

        limited.MapPost("/refresh", async (RefreshTokenRequest r, IAuthService s, CancellationToken ct) =>
                (await s.RefreshAsync(r, ct)).ToHttpResult())
            .AllowAnonymous();

        limited.MapPost("/logout", async (RefreshTokenRequest r, IAuthService s, CancellationToken ct) =>
                (await s.LogoutAsync(r, ct)).ToHttpResult())
            .AllowAnonymous();

        limited.MapPost("/change-password", async (ChangePasswordRequest r, IAuthService s, CancellationToken ct) =>
                (await s.ChangePasswordAsync(r, ct)).ToHttpResult())
            .RequireAuthorization();

        // Email verification
        limited.MapPost("/verify-email", async (VerifyEmailRequest r, IEmailVerificationService s, CancellationToken ct) =>
                (await s.VerifyAsync(r, ct)).ToHttpResult())
            .AllowAnonymous();

        limited.MapPost("/send-verification", async (IEmailVerificationService s, CancellationToken ct) =>
                (await s.SendToCurrentUserAsync(ct)).ToHttpResult())
            .RequireAuthorization();

        limited.MapPost("/resend-verification", async (ResendVerificationRequest r, IEmailVerificationService s, CancellationToken ct) =>
                (await s.ResendByEmailAsync(r, ct)).ToHttpResult())
            .AllowAnonymous();
    }

    private static void MapUsers(RouteGroupBuilder users, string basePath)
    {
        users.MapGet("/", async (
                string? search,
                bool? isActive,
                int? page,
                int? pageSize,
                IUserService s,
                CancellationToken ct) =>
        {
            var query = new UserListQuery
            {
                Search = search,
                IsActive = isActive,
                Page = page ?? 1,
                PageSize = pageSize ?? 20
            };

            return (await s.ListAsync(query, ct)).ToHttpResult();
        })
            .RequirePermission(IamPermissionNames.UsersRead);

        users.MapGet("/{id:guid}", async (Guid id, IUserService s, CancellationToken ct) =>
                (await s.GetAsync(id, ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.UsersRead);

        users.MapPost("/", async (CreateUserRequest r, IUserService s, CancellationToken ct) =>
        {
            var result = await s.CreateAsync(r, ct);

            return result.IsSuccess
                ? Results.Created($"{basePath}/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        })
            .RequirePermission(IamPermissionNames.UsersCreate);

        users.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest r, IUserService s, CancellationToken ct) =>
                (await s.UpdateAsync(id, r, ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.UsersUpdate);

        users.MapPut("/{id:guid}/roles", async (Guid id, SetUserRolesRequest r, IUserService s, CancellationToken ct) =>
                (await s.SetRolesAsync(id, r, ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.UsersUpdate);

        users.MapDelete("/{id:guid}", async (Guid id, IUserService s, CancellationToken ct) =>
                (await s.DeleteAsync(id, ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.UsersDelete);
    }

    private static void MapRoles(RouteGroupBuilder roles, string basePath)
    {
        roles.MapGet("/", async (IRoleService s, CancellationToken ct) =>
                (await s.ListAsync(ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.RolesRead);

        roles.MapGet("/{id:guid}", async (Guid id, IRoleService s, CancellationToken ct) =>
                (await s.GetAsync(id, ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.RolesRead);

        roles.MapPost("/", async (CreateRoleRequest r, IRoleService s, CancellationToken ct) =>
        {
            var result = await s.CreateAsync(r, ct);

            return result.IsSuccess
                ? Results.Created($"{basePath}/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        })
            .RequirePermission(IamPermissionNames.RolesCreate);

        roles.MapPut("/{id:guid}", async (Guid id, UpdateRoleRequest r, IRoleService s, CancellationToken ct) =>
                (await s.UpdateAsync(id, r, ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.RolesUpdate);

        roles.MapPut("/{id:guid}/permissions", async (Guid id, SetRolePermissionsRequest r, IRoleService s, CancellationToken ct) =>
                (await s.SetPermissionsAsync(id, r, ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.RolesUpdate);

        roles.MapDelete("/{id:guid}", async (Guid id, IRoleService s, CancellationToken ct) =>
                (await s.DeleteAsync(id, ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.RolesDelete);
    }

    private static void MapPermissions(RouteGroupBuilder permissions)
    {
        permissions.MapGet("/", async (IPermissionService s, CancellationToken ct) =>
                (await s.ListAsync(ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.PermissionsRead);

        permissions.MapGet("/{id:guid}", async (Guid id, IPermissionService s, CancellationToken ct) =>
                (await s.GetAsync(id, ct)).ToHttpResult())
            .RequirePermission(IamPermissionNames.PermissionsRead);
    }
}