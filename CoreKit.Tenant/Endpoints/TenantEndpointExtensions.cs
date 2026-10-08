using CoreKit.IAM.Authorization;
using Microsoft.AspNetCore.Routing;

namespace CoreKit.Tenant.Endpoints;

public static class TenantEndpointExtensions
{
    /// <summary>
    /// Maps the tenant HTTP API under a prefix (default "/tenants"). Optional: an app that wants its own
    /// routes can skip this and call the services directly.
    /// </summary>
    public static IEndpointRouteBuilder MapTenants(this IEndpointRouteBuilder app, string prefix = "/tenants")
    {
        prefix = NormalizePrefix(prefix);

        var tenants = app.MapGroup(prefix).WithTags("Tenants");

        MapTenantRoutes(tenants, prefix);
        MapSettingRoutes(tenants.MapGroup("/{tenantId:guid}/settings"));
        MapMemberRoutes(tenants.MapGroup("/{tenantId:guid}/members"));

        tenants.MapGet("/{tenantId:guid}/audit", async (
                Guid tenantId, int? page, int? pageSize, ITenantAuditLog s, CancellationToken ct) =>
            (await s.ListAsync(tenantId, new TenantAuditQuery { Page = page ?? 1, PageSize = pageSize ?? 50 }, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.AuditRead);

        return app;
    }

    private static string NormalizePrefix(string? prefix)
    {
        var trimmed = (prefix ?? string.Empty).Trim().TrimEnd('/');

        return trimmed.Length == 0 || trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    private static void MapTenantRoutes(RouteGroupBuilder tenants, string basePath)
    {
        tenants.MapGet("/", async (
                string? search, TenantStatus? status, int? page, int? pageSize, ITenantService s, CancellationToken ct) =>
            (await s.ListAsync(new TenantListQuery
            {
                Search = search,
                Status = status,
                Page = page ?? 1,
                PageSize = pageSize ?? 20
            }, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.Read);

        // The tenant of the current request. Any signed-in member may read it.
        tenants.MapGet("/current", async (ITenantService s, CancellationToken ct) =>
                (await s.GetCurrentAsync(ct)).ToHttpResult())
            .RequireAuthorization()
            .RequireTenant();

        // The signed-in user's own tenants, and which one is their default.
        tenants.MapGet("/me", async (ITenantActor actor, ITenantMembershipService s, CancellationToken ct) =>
        {
            var result = actor.UserId is { } userId
                ? await s.ListForUserAsync(userId, ct)
                : TenantResult.Failure<IReadOnlyList<TenantMembershipDto>>(TenantErrors.NotAuthenticated);

            return result.ToHttpResult();
        }).RequireAuthorization();

        tenants.MapPut("/me/default/{tenantId:guid}", async (
                Guid tenantId, ITenantActor actor, ITenantMembershipService s, CancellationToken ct) =>
        {
            var result = actor.UserId is { } userId
                ? await s.SetDefaultTenantAsync(userId, tenantId, ct)
                : TenantResult.Failure(TenantErrors.NotAuthenticated);

            return result.ToHttpResult();
        }).RequireAuthorization();

        tenants.MapGet("/{id:guid}", async (Guid id, ITenantService s, CancellationToken ct) =>
                (await s.GetAsync(id, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.Read);

        tenants.MapPost("/", async (CreateTenantRequest r, ITenantService s, CancellationToken ct) =>
        {
            var result = await s.CreateAsync(r, ct);

            return result.IsSuccess
                ? Results.Created($"{basePath}/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequirePermission(TenantPermissionNames.Create);

        tenants.MapPut("/{id:guid}", async (Guid id, UpdateTenantRequest r, ITenantService s, CancellationToken ct) =>
                (await s.UpdateAsync(id, r, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.Update);

        tenants.MapPost("/{id:guid}/activate", async (Guid id, ITenantService s, CancellationToken ct) =>
                (await s.ActivateAsync(id, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.ManageStatus);

        tenants.MapPost("/{id:guid}/suspend", async (Guid id, ChangeTenantStatusRequest? r, ITenantService s, CancellationToken ct) =>
                (await s.SuspendAsync(id, r ?? new ChangeTenantStatusRequest(), ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.ManageStatus);

        tenants.MapPost("/{id:guid}/archive", async (Guid id, ChangeTenantStatusRequest? r, ITenantService s, CancellationToken ct) =>
                (await s.ArchiveAsync(id, r ?? new ChangeTenantStatusRequest(), ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.ManageStatus);

        tenants.MapPost("/{id:guid}/restore", async (Guid id, ITenantService s, CancellationToken ct) =>
                (await s.RestoreAsync(id, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.ManageStatus);

        tenants.MapDelete("/{id:guid}", async (Guid id, ITenantService s, CancellationToken ct) =>
                (await s.DeleteAsync(id, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.Delete);
    }

    private static void MapSettingRoutes(RouteGroupBuilder settings)
    {
        settings.MapGet("/", async (Guid tenantId, ITenantSettingsService s, CancellationToken ct) =>
                (await s.ListAsync(tenantId, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.SettingsRead);

        settings.MapGet("/{key}", async (Guid tenantId, string key, ITenantSettingsService s, CancellationToken ct) =>
                (await s.GetAsync(tenantId, key, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.SettingsRead);

        settings.MapPut("/{key}", async (
                Guid tenantId, string key, SetTenantSettingRequest r, ITenantSettingsService s, CancellationToken ct) =>
            (await s.SetAsync(tenantId, key, r, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.SettingsUpdate);

        settings.MapPut("/", async (
                Guid tenantId, SetTenantSettingsRequest r, ITenantSettingsService s, CancellationToken ct) =>
            (await s.SetManyAsync(tenantId, r, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.SettingsUpdate);

        settings.MapDelete("/{key}", async (Guid tenantId, string key, ITenantSettingsService s, CancellationToken ct) =>
                (await s.RemoveAsync(tenantId, key, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.SettingsUpdate);
    }

    private static void MapMemberRoutes(RouteGroupBuilder members)
    {
        members.MapGet("/", async (Guid tenantId, ITenantMembershipService s, CancellationToken ct) =>
                (await s.ListMembersAsync(tenantId, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.MembersRead);

        members.MapPost("/", async (
                Guid tenantId, AddTenantMemberRequest r, ITenantMembershipService s, CancellationToken ct) =>
            (await s.AddMemberAsync(tenantId, r, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.MembersManage);

        members.MapDelete("/{userId:guid}", async (
                Guid tenantId, Guid userId, ITenantMembershipService s, CancellationToken ct) =>
            (await s.RemoveMemberAsync(tenantId, userId, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.MembersManage);

        members.MapPut("/{userId:guid}/owner", async (
                Guid tenantId, Guid userId, SetTenantOwnerRequest r, ITenantMembershipService s, CancellationToken ct) =>
            (await s.SetOwnerAsync(tenantId, userId, r, ct)).ToHttpResult())
            .RequirePermission(TenantPermissionNames.MembersManage);
    }
}