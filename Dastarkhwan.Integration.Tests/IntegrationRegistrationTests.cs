using CoreKit.Access.Contracts;
using CoreKit.Access.Services;
using CoreKit.Identity.Contracts;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Interfaces;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Common;
using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Models;
using CoreKit.Tenant.Persistence;
using Dastarkhwan.Integration.Membership;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dastarkhwan.Integration.Tests;

public sealed class IntegrationRegistrationTests
{
    private static ServiceCollection ServicesWithAccessPorts()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantMembershipReader, FakeTenantMembershipReader>();
        services.AddScoped<IAccessMaintenance, FakeAccessMaintenance>();
        services.AddScoped<ICurrentTenant, FakeCurrentTenant>();
        services.AddScoped<ITenantActor, FakeTenantActor>();
        // Internal services required by integration handlers
        services.AddScoped<ITenantMembershipService, FakeTenantMembershipService>();
        services.AddScoped<ITenantMemberRepository, FakeTenantMemberRepository>();
        services.AddScoped<IUserRepository, FakeUserRepository>();
        // Required for AccessPlatformAdministratorProvider
        services.AddScoped<IAccessService, FakeAccessService>();
        services.AddSingleton<IPlatformAdministratorProvider, FakePlatformAdministratorProvider>();
        return services;
    }

    [Fact]
    public void Registers_integration_services_and_resolves_them()
    {
        var services = ServicesWithAccessPorts();
        services.AddDastarkhwanIntegration();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Verify event handlers are registered for each event type
        // Note: IdentityUserDeleted has two handlers but TryAddScoped only registers the first
        var identityUserDeletedHandler = scope.ServiceProvider.GetService<IIdentityEventHandler<IdentityUserDeleted>>();
        Assert.NotNull(identityUserDeletedHandler);

        var tenantCreatedHandler = scope.ServiceProvider.GetService<ITenantIntegrationEventHandler<TenantCreated>>();
        Assert.NotNull(tenantCreatedHandler);

        var tenantMemberRemovedHandler = scope.ServiceProvider.GetService<ITenantIntegrationEventHandler<TenantMemberRemoved>>();
        Assert.NotNull(tenantMemberRemovedHandler);

        var tenantDeletedHandler = scope.ServiceProvider.GetService<ITenantIntegrationEventHandler<TenantDeleted>>();
        Assert.NotNull(tenantDeletedHandler);

        // Verify other integration services
        var claimsContributor = scope.ServiceProvider.GetService<IAccessTokenClaimsContributor>();
        Assert.NotNull(claimsContributor);

        var ownerUserValidator = scope.ServiceProvider.GetService<CoreKit.Tenant.Abstractions.IOwnerUserValidator>();
        Assert.NotNull(ownerUserValidator);

        var userDeletionGuard = scope.ServiceProvider.GetService<CoreKit.Identity.Contracts.IUserDeletionGuard>();
        Assert.NotNull(userDeletionGuard);
    }

    [Fact]
    public void Calling_twice_does_not_duplicate_registrations()
    {
        var services = ServicesWithAccessPorts();

        services.AddDastarkhwanIntegration();
        services.AddDastarkhwanIntegration();

        // Should not throw on build
        using var provider = services.BuildServiceProvider();
    }

    private sealed class FakeTenantMembershipReader : ITenantMembershipReader
    {
        public Task<TenantMemberInfo?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult<TenantMemberInfo?>(null);

        public Task<IReadOnlyList<(Guid TenantId, bool IsOwner, long MembershipGeneration)>> ListForUserIncludingRemovedAsync(
            Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<(Guid TenantId, bool IsOwner, long MembershipGeneration)>>(new List<(Guid TenantId, bool IsOwner, long MembershipGeneration)>());
    }

    private sealed class FakeAccessMaintenance : IAccessMaintenance
    {
        public Task RevokeTenantMembershipAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveUserFromAllScopesAsync(Guid userId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveScopeAsync(AccessScope scope, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task ProvisionScopeAsync(AccessScope scope, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task AddPlatformMemberAsync(Guid userId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemovePlatformMemberAsync(Guid userId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SetPlatformUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCurrentTenant : ICurrentTenant
    {
        public Guid? TenantId { get; set; }

        public bool IsResolved => TenantId.HasValue;

        public Guid? Id => TenantId;

        public Guid RequiredId => TenantId ?? throw new TenantNotResolvedException();

        public IDisposable Change(Guid? tenantId)
        {
            var old = TenantId;
            TenantId = tenantId;
            return new DisposableAction(() => TenantId = old);
        }

        private sealed class DisposableAction : IDisposable
        {
            private readonly Action _action;
            public DisposableAction(Action action) => _action = action;
            public void Dispose() => _action();
        }
    }

    private sealed class FakeTenantActor : ITenantActor
    {
        public Guid? UserId { get; set; }

        public bool IsAuthenticated => UserId.HasValue;

        public bool IsPlatformAdmin { get; set; }

        public Task<bool> IsPlatformAdminAsync(CancellationToken ct = default) => Task.FromResult(IsPlatformAdmin);
    }

    private sealed class FakeTenantMembershipService : ITenantMembershipService
    {
        public Task<TenantResult<IReadOnlyList<TenantMemberDto>>> ListMembersAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(TenantResult.Success<IReadOnlyList<TenantMemberDto>>(new List<TenantMemberDto>()));

        public Task<TenantResult<TenantMemberDto>> AddMemberAsync(Guid tenantId, AddTenantMemberRequest request, CancellationToken ct = default)
            => Task.FromResult(TenantResult.Success<TenantMemberDto>(new TenantMemberDto
            {
                TenantId = tenantId,
                UserId = request.UserId,
                IsOwner = request.IsOwner,
                IsDefault = false,
                JoinedAt = DateTime.UtcNow
            }));

        public Task<TenantResult> RemoveMemberAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(TenantResult.Success());

        public Task<TenantResult<TenantMemberDto>> SetOwnerAsync(Guid tenantId, Guid userId, SetTenantOwnerRequest request, CancellationToken ct = default)
            => Task.FromResult(TenantResult.Success<TenantMemberDto>(new TenantMemberDto
            {
                TenantId = tenantId,
                UserId = userId,
                IsOwner = true,
                IsDefault = false,
                JoinedAt = DateTime.UtcNow
            }));

        public Task<TenantResult<IReadOnlyList<TenantMembershipDto>>> ListForUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(TenantResult.Success<IReadOnlyList<TenantMembershipDto>>(new List<TenantMembershipDto>()));

        public Task<TenantResult> SetDefaultTenantAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(TenantResult.Success());

        public Task<bool> IsMemberAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<int> RemoveAllMembershipsForDeletedUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(0);
    }

    private sealed class FakeTenantMemberRepository : ITenantMemberRepository
    {
        public Task<TenantMember?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult<TenantMember?>(null);

        public Task<TenantMember?> FindForUpdateAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult<TenantMember?>(null);

        public Task<TenantMember?> FindIncludingRemovedAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult<TenantMember?>(null);

        public Task<IReadOnlyList<TenantMember>> ListAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TenantMember>>(new List<TenantMember>());

        public Task<IReadOnlyList<TenantMember>> ListForUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TenantMember>>(new List<TenantMember>());

        public Task<IReadOnlyList<TenantMember>> ListForUserForUpdateAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TenantMember>>(new List<TenantMember>());

        public Task<IReadOnlyList<TenantMember>> ListForUserIncludingRemovedAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TenantMember>>(new List<TenantMember>());

        public Task<int> CountOwnersAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(0);

        public Task<bool> HasDefaultAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<Guid?> FindPreferredTenantIdAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<Guid?>(null);

        public void Add(TenantMember member) { }

        public void Remove(TenantMember member) { }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<User?>(null);

        public Task<User?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default)
            => Task.FromResult<User?>(null);

        public Task<User?> FindForUpdateAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<User?>(null);

        public Task<Guid?> FindUnconfirmedActiveIdByEmailAsync(string normalizedEmail, CancellationToken ct = default)
            => Task.FromResult<Guid?>(null);

        public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId = null, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<bool> MobileExistsAsync(string normalizedMobile, Guid? excludingUserId = null, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<(IReadOnlyList<User> Items, int TotalCount)> ListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<User> Items, int TotalCount)>((new List<User>(), 0));

        public Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(new List<Guid>());

        public Task<bool> CreateAsync(User user, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> UpdateAsync(User user, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> RegisterFailedLoginAttemptAsync(Guid userId, DateTime now, DateTime lockoutEnd, int threshold, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<LoginCompletion> TryCompleteSuccessfulLoginAsync(Guid userId, int expectedSecurityVersion, DateTime now, CancellationToken ct = default)
            => Task.FromResult(LoginCompletion.Completed);

        public Task<CredentialUpdateResult> TryChangePasswordAsync(Guid userId, string expectedCurrentHash, int expectedSecurityVersion, string newHash, DateTime now, bool bumpSecurityVersion, bool revokeSessions, CancellationToken ct = default)
            => Task.FromResult(CredentialUpdateResult.Updated);

        public Task<CredentialUpdateResult> TryDeactivateAsync(Guid userId, int expectedSecurityVersion, DateTime now, CancellationToken ct = default)
            => Task.FromResult(CredentialUpdateResult.Updated);

        public Task<bool> TryRehashPasswordAsync(Guid userId, string expectedCurrentHash, int expectedSecurityVersion, string newHash, DateTime now, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<CredentialUpdateResult> TryDeactivateAndChangePasswordAsync(Guid userId, int expectedSecurityVersion, string expectedCurrentHash, string newHash, DateTime now, CancellationToken ct = default)
            => Task.FromResult(CredentialUpdateResult.Updated);

        public Task DeleteAsync(User user, CancellationToken ct = default)
            => Task.CompletedTask;

        public void StageDelete(User user) { }
    }

    private sealed class FakeAccessService : IAccessService
    {
        public Task<EffectiveAccess> GetAccessAsync(Guid userId, AccessScope scope, CancellationToken ct = default)
            => Task.FromResult(EffectiveAccess.None);

        public Task<bool> HasPermissionAsync(Guid userId, AccessScope scope, string permission, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<AccessResult<EffectiveAccess>> GetMyAccessAsync(CancellationToken ct = default)
            => Task.FromResult(AccessResult.Success(EffectiveAccess.None));

        public Task<IReadOnlyList<Guid>> ListUserIdsWithRoleAsync(AccessScope scope, string roleName, long? membershipGeneration, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(new List<Guid>());

        public Task<IReadOnlyList<AccessRoleDto>> ListRolesAsync(AccessScope scope, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AccessRoleDto>>(new List<AccessRoleDto>());

        public Task<EffectiveAccess> GetFreshAccessAsync(Guid userId, AccessScope scope, CancellationToken ct = default)
            => Task.FromResult(EffectiveAccess.None);
    }

    private sealed class FakePlatformAdministratorProvider : IPlatformAdministratorProvider
    {
        public Task<IReadOnlyList<Guid>> GetPlatformAdministratorUserIdsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(new List<Guid>());
    }
}