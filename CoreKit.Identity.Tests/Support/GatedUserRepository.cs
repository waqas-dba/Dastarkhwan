using CoreKit.Identity.Interfaces;

namespace CoreKit.Identity.Tests.Support;

/// <summary>
/// Wraps a real <see cref="IUserRepository"/> and runs a hook immediately before the
/// authoritative successful-login transition. A test uses this to park the login request at
/// the exact point where the old implementation relied on a stale snapshot, let a concurrent
/// request establish a lockout, and then release the transition — a deterministic race
/// ordering with no timing-based sleeps.
/// </summary>
internal sealed class GatedUserRepository : IUserRepository
{
    private readonly IUserRepository _inner;
    private readonly Func<Task> _beforeSuccessTransition;

    public GatedUserRepository(IUserRepository inner, Func<Task> beforeSuccessTransition)
    {
        _inner = inner;
        _beforeSuccessTransition = beforeSuccessTransition;
    }

    public async Task<bool> TryCompleteSuccessfulLoginAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        await _beforeSuccessTransition();
        return await _inner.TryCompleteSuccessfulLoginAsync(userId, now, ct);
    }

    public Task<bool> RegisterFailedLoginAttemptAsync(
        Guid userId, DateTime now, DateTime lockoutEnd, int threshold, CancellationToken ct = default)
        => _inner.RegisterFailedLoginAttemptAsync(userId, now, lockoutEnd, threshold, ct);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _inner.FindByIdAsync(id, ct);

    public Task<User?> FindByIdWithRolesAsync(Guid id, CancellationToken ct = default)
        => _inner.FindByIdWithRolesAsync(id, ct);

    public Task<User?> FindForUpdateAsync(Guid id, CancellationToken ct = default)
        => _inner.FindForUpdateAsync(id, ct);

    public Task<User?> FindByNormalizedEmailWithRolesAsync(string normalizedEmail, CancellationToken ct = default)
        => _inner.FindByNormalizedEmailWithRolesAsync(normalizedEmail, ct);

    public Task<Guid?> FindUnconfirmedActiveIdByEmailAsync(string normalizedEmail, CancellationToken ct = default)
        => _inner.FindUnconfirmedActiveIdByEmailAsync(normalizedEmail, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId = null, CancellationToken ct = default)
        => _inner.EmailExistsAsync(normalizedEmail, excludingUserId, ct);

    public Task<bool> MobileExistsAsync(string normalizedMobile, Guid? excludingUserId = null, CancellationToken ct = default)
        => _inner.MobileExistsAsync(normalizedMobile, excludingUserId, ct);

    public Task<(IReadOnlyList<User> Items, int TotalCount)> ListAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
        => _inner.ListAsync(search, isActive, page, pageSize, ct);

    public Task<IReadOnlyList<Guid>> GetActiveUserIdsInRoleAsync(string normalizedRoleName, CancellationToken ct = default)
        => _inner.GetActiveUserIdsInRoleAsync(normalizedRoleName, ct);

    public Task<bool> CreateAsync(User user, CancellationToken ct = default)
        => _inner.CreateAsync(user, ct);

    public Task<bool> UpdateAsync(User user, CancellationToken ct = default)
        => _inner.UpdateAsync(user, ct);

    public Task SetPasswordHashAsync(Guid userId, string passwordHash, DateTime now, CancellationToken ct = default)
        => _inner.SetPasswordHashAsync(userId, passwordHash, now, ct);

    public Task ReplaceRolesAsync(User user, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default)
        => _inner.ReplaceRolesAsync(user, roleIds, ct);

    public Task DeleteAsync(User user, CancellationToken ct = default)
        => _inner.DeleteAsync(user, ct);
}