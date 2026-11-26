using CoreKit.Abstractions.IntegrationEvents;
using CoreKit.Access.Abstractions;
using CoreKit.Access.Contracts;
using CoreKit.Access.Extensions;
using CoreKit.Identity.Contracts;
using CoreKit.Identity.Outbox;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Constants;
using CoreKit.Tenant.Outbox;
using CoreKit.Tenant.Persistence;
using Dastarkhwan.Integration.Authorization;
using Dastarkhwan.Integration.Identity;
using Dastarkhwan.Integration.Membership;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dastarkhwan.Integration;

public static class IntegrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Tenant-to-Access integration adapters. Call it after the host has registered Access
    /// (AddAccess) and Tenant. It does not register those modules, so a missing module fails when the
    /// adapters are resolved rather than being silently replaced by a fallback.
    ///
    /// Registrations use TryAdd, so a host's own registration wins and calling this twice adds nothing.
    /// Lifetimes are scoped because Access's consumer, installer and reader are scoped; the Tenant dispatcher
    /// resolves the transport inside its own scope for every delivery.
    ///
    /// This does not start the outbox publisher and does not run projection bootstrap. Those arrive with the
    /// projection cutover and must be in place before Access authorizes from the projection.
    /// </summary>
    public static IServiceCollection AddDastarkhwanIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Identity outbox transport for durable account deletion events.
        // Identity registers NullIdentityOutboxTransport by default; replace it so cleanup actually runs.
        services.RemoveAll<IIdentityOutboxTransport>();
        services.AddScoped<IIdentityOutboxTransport, IdentityOutboxTransport>();

        // Cross-module components moved from Tenant to Integration

        // Declare Tenant's permissions in the Access catalog so Access can grant them to tenant roles.
        services.AddAccessPermissions(p =>
        {
            foreach (var permission in TenantPermissionNames.All)
                p.Add(permission.Name, permission.Description);
        });

        // Access queries Tenant membership directly through ITenantMembershipReader (see Access.Services.AccessService).
        // When a person leaves a tenant, clear their effective-authorization state in Access so a
        // cached tenant-scoped permission set cannot outlive the membership.
        services.TryAddScoped<IEventHandler<TenantMemberRemoved>, TenantMemberRemovedToAccessMaintenance>();

        // When a tenant is created, provision its default roles in Access.
        services.TryAddScoped<IEventHandler<TenantCreated>, TenantCreatedToAccessProvision>();

        // When an Identity user is deleted, clean up their Tenant membership rows and Access platform membership.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEventHandler<IdentityUserDeleted>, IdentityUserDeletedToTenantCleanup>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEventHandler<IdentityUserDeleted>, IdentityUserDeletedToAccessCleanup>());

        // Tenant adds its tenant claim to every access token Identity issues.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAccessTokenClaimsContributor, TenantClaimsContributor>());

        // Add platform admin claim to access tokens for platform administrators.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAccessTokenClaimsContributor, PlatformAdminClaimsContributor>());

        // Bridge Tenant's current user abstraction to Identity's IAccessCurrentUser.
        services.TryAddScoped<ITenantCurrentUser>(sp =>
        {
            var accessCurrentUser = sp.GetService<IAccessCurrentUser>();
            return accessCurrentUser is not null ? new TenantCurrentUserBridge(accessCurrentUser) : null!;
        });

        // Bridge Tenant's permission checker to Access's IPermissionChecker.
        services.TryAddScoped<ITenantPermissionChecker>(sp =>
        {
            var permissionChecker = sp.GetService<IPermissionChecker>();
            return permissionChecker is not null ? new TenantPermissionCheckerBridge(permissionChecker) : null!;
        });

        // Validate that a tenant owner user exists and is active in Identity.
        services.TryAddScoped<CoreKit.Tenant.Abstractions.IOwnerUserValidator, IdentityOwnerUserValidator>();

        // Guard against deleting a user who is a tenant owner.
        services.TryAddScoped<IUserDeletionGuard, UserDeletionGuard>();

        return services;
    }

    private sealed class TenantCurrentUserBridge : ITenantCurrentUser
    {
        private readonly IAccessCurrentUser _inner;

        public TenantCurrentUserBridge(IAccessCurrentUser inner) => _inner = inner;

        public Guid? UserId => _inner.UserId;

        public bool IsAuthenticated => _inner.IsAuthenticated;
    }

    private sealed class TenantPermissionCheckerBridge : ITenantPermissionChecker
    {
        private readonly IPermissionChecker _inner;

        public TenantPermissionCheckerBridge(IPermissionChecker inner) => _inner = inner;

        public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
            => _inner.HasPermissionAsync(permission, ct);

        public Task<bool> HasFreshPermissionAsync(string permission, CancellationToken ct = default)
            => _inner.HasFreshPermissionAsync(permission, ct);

        public Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default)
            => _inner.HasPlatformPermissionAsync(permission, ct);
    }
}