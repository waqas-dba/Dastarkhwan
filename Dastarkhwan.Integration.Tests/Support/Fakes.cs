using CoreKit.Access.Abstractions;
using CoreKit.Tenant.Abstractions;
using CoreKit.Tenant.Entities;
using CoreKit.Tenant.Persistence;

namespace Dastarkhwan.Integration.Tests.Support;

internal sealed class FakeMembershipEventConsumer : IAccessMembershipEventConsumer
{
    public AccessMembershipEventResult Result { get; set; } = AccessMembershipEventResult.Applied();
    public Exception? Throw { get; set; }
    public AccessMembershipEventEnvelope? Received { get; private set; }
    public CancellationToken ReceivedToken { get; private set; }
    public int Calls { get; private set; }

    public Task<AccessMembershipEventResult> ApplyAsync(
        AccessMembershipEventEnvelope envelope, CancellationToken ct = default)
    {
        Calls++;
        Received = envelope;
        ReceivedToken = ct;
        if (Throw is not null)
            throw Throw;
        return Task.FromResult(Result);
    }
}

internal sealed class FakeSnapshotInstaller : IAccessMembershipSnapshotInstaller
{
    public AccessSnapshotOutcome Outcome { get; set; } = AccessSnapshotOutcome.Installed;
    public AccessMembershipSnapshot? Received { get; private set; }

    public Task<AccessSnapshotOutcome> InstallAsync(
        AccessMembershipSnapshot snapshot, CancellationToken ct = default)
    {
        Received = snapshot;
        return Task.FromResult(Outcome);
    }
}

internal sealed class FakeProjectionReader : IAccessMembershipProjectionReader
{
    public AccessTenantProjectionView? View { get; set; }
    public IReadOnlyList<Guid> TenantIds { get; set; } = Array.Empty<Guid>();

    public Task<AccessTenantProjectionView?> GetTenantViewAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(View);

    public Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken ct = default)
        => Task.FromResult(TenantIds);
}

internal sealed class FakeTenantMembershipReader : ITenantMembershipReader
{
    public Task<TenantMemberInfo?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<TenantMemberInfo?>(null);

    public Task<long> GetLastEventSequenceAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(0L);

    public Task<long> GetLastAppliedSequenceAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(0L);
}

internal sealed class FakeTenantMemberRepository : ITenantMemberRepository
{
    public Task<TenantMember?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<TenantMember?>(null);

    public Task<TenantMember?> FindForUpdateAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<TenantMember?>(null);

    public Task<TenantMember?> FindIncludingRemovedAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<TenantMember?>(null);

    public Task<IReadOnlyList<TenantMember>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TenantMember>>(Array.Empty<TenantMember>());

    public Task<IReadOnlyList<TenantMember>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TenantMember>>(Array.Empty<TenantMember>());

    public Task<IReadOnlyList<TenantMember>> ListForUserForUpdateAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TenantMember>>(Array.Empty<TenantMember>());

    public Task<int> CountOwnersAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<bool> HasDefaultAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<Guid?> FindPreferredTenantIdAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<Guid?>(null);

    public void Add(TenantMember member) { }

    public void Remove(TenantMember member) { }
}

internal sealed class FakeAccessMaintenance : IAccessMaintenance
{
    public Task RevokeTenantMembershipAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RemoveUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
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

    public Task<Guid> EnsurePlatformRoleAsync(string name, string? description, IReadOnlyCollection<string> permissions, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());
}