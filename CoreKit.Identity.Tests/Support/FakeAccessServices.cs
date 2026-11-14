using CoreKit.Access.Abstractions;
using CoreKit.Access.Common;
using CoreKit.Access.Models;
using CoreKit.Identity.Constants;

namespace CoreKit.Identity.Tests.Support;

/// <summary>
/// In-memory state backing the Access fakes. Identity no longer owns roles or permissions; it asks Access
/// (through IPermissionChecker / IAccessService / IAccessMaintenance) whether the signed-in user may do
/// something in the fixed platform scope, and delegates platform membership and role assignment to Access.
/// The tests configure this state directly instead of spinning up a real Access database: the Identity
/// suite covers Identity logic (user CRUD, credential guards, lockout), and Access internals are covered
/// by the Access suite.
/// </summary>
internal sealed class FakeAccessState
{
    private readonly Dictionary<Guid, EffectiveAccess> _byUser = new();

    public List<AccessRoleDto> PlatformRoles { get; } = new();

    /// <summary>The user whose platform permissions the IPermissionChecker answers for.</summary>
    public Guid? CurrentUserId { get; set; }

    public EffectiveAccess Get(Guid userId)
        => _byUser.TryGetValue(userId, out var access) ? access : EffectiveAccess.None;

    public void SetAccess(
        Guid userId, bool isMember, bool isOwner, IEnumerable<string> permissions, IEnumerable<string> roles)
        => _byUser[userId] = new EffectiveAccess(
            isMember, isOwner,
            new HashSet<string>(permissions, StringComparer.Ordinal),
            roles.ToList());

    public void AddPlatformMember(Guid userId)
    {
        var current = Get(userId);
        _byUser[userId] = new EffectiveAccess(true, current.IsOwner, current.Permissions, current.Roles);
    }

    public void RemovePlatformMember(Guid userId)
    {
        var current = Get(userId);
        _byUser[userId] = new EffectiveAccess(false, false, current.Permissions, Array.Empty<string>());
    }

    public void SetPlatformRoles(Guid userId, IReadOnlyCollection<Guid> roleIds)
    {
        var names = PlatformRoles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Name)
            .ToList();

        var current = Get(userId);
        _byUser[userId] = new EffectiveAccess(
            current.IsMember || current.IsOwner, current.IsOwner, current.Permissions, names);
    }

    public IReadOnlyList<Guid> UsersWithRole(string roleName)
        => _byUser
            .Where(kv => kv.Value.IsMember && kv.Value.Roles.Contains(roleName, StringComparer.Ordinal))
            .Select(kv => kv.Key)
            .ToList();
}

/// <summary>
/// Fakes the platform permission check. Identity user management is platform-level, so it always asks
/// HasPlatformPermissionAsync; the fake answers against the current user's configured platform access.
/// </summary>
internal sealed class FakePermissionChecker : CoreKit.Access.Abstractions.IPermissionChecker
{
    private readonly FakeAccessState _state;

    public FakePermissionChecker(FakeAccessState state) => _state = state;

    public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
        => Task.FromResult(_state.CurrentUserId is { } id && _state.Get(id).Has(permission));

    public Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default)
        => Task.FromResult(_state.CurrentUserId is { } id && _state.Get(id).Has(permission));
}

/// <summary>
/// Fakes Access. Returns per-user access, the platform role catalogue and the users holding a named role.
/// Backed by the same <see cref="FakeAccessState"/> the maintenance fake mutates, so a create or role
/// change is visible to the DTO built immediately afterwards.
/// </summary>
internal sealed class FakeAccessService : IAccessService
{
    private readonly FakeAccessState _state;

    public FakeAccessService(FakeAccessState state) => _state = state;

    public Task<EffectiveAccess> GetAccessAsync(Guid userId, AccessScope scope, CancellationToken ct = default)
        => Task.FromResult(_state.Get(userId));

    public Task<bool> HasPermissionAsync(Guid userId, AccessScope scope, string permission, CancellationToken ct = default)
        => Task.FromResult(_state.Get(userId).Has(permission));

    public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default)
        => Task.FromResult(_state.CurrentUserId is { } id && _state.Get(id).Has(permission));

    public Task<AccessResult<EffectiveAccess>> GetMyAccessAsync(CancellationToken ct = default)
    {
        if (_state.CurrentUserId is not { } id)
            return Task.FromResult(AccessResult.Failure<EffectiveAccess>(
                new AccessError(AccessErrorKind.Unauthorized, "unauthenticated", "No signed-in user.")));

        return Task.FromResult(AccessResult.Success(_state.Get(id)));
    }

    public Task<IReadOnlyList<Guid>> ListUserIdsWithRoleAsync(AccessScope scope, string roleName, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(_state.UsersWithRole(roleName));

    public Task<IReadOnlyList<AccessRoleDto>> ListRolesAsync(AccessScope scope, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AccessRoleDto>>(_state.PlatformRoles);
}

/// <summary>
/// Fakes Access maintenance. Identity calls this on user create / deactivation / delete to keep platform
/// membership and role assignments in step; the fake mutates the shared <see cref="FakeAccessState"/> so
/// the DTO built after a write reflects the new roles.
/// </summary>
internal sealed class FakeAccessMaintenance : IAccessMaintenance
{
    private readonly FakeAccessState _state;

    public FakeAccessMaintenance(FakeAccessState state) => _state = state;

    public Task RemoveUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RemoveScopeAsync(AccessScope scope, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ProvisionScopeAsync(AccessScope scope, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task AddPlatformMemberAsync(Guid userId, CancellationToken ct = default)
    {
        _state.AddPlatformMember(userId);
        return Task.CompletedTask;
    }

    public Task RemovePlatformMemberAsync(Guid userId, CancellationToken ct = default)
    {
        _state.RemovePlatformMember(userId);
        return Task.CompletedTask;
    }

    public Task SetPlatformUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default)
    {
        _state.SetPlatformRoles(userId, roleIds);
        return Task.CompletedTask;
    }

    public Task<Guid> EnsurePlatformRoleAsync(
        string name, string? description, IReadOnlyCollection<string> permissions, CancellationToken ct = default)
    {
        var existing = _state.PlatformRoles.FirstOrDefault(r => r.Name == name);

        if (existing is not null)
            return Task.FromResult(existing.Id);

        var role = new AccessRoleDto
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Permissions = permissions.ToList()
        };

        _state.PlatformRoles.Add(role);
        return Task.FromResult(role.Id);
    }
}