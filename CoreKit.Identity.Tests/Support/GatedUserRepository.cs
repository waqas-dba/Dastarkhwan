using CoreKit.Identity.Interfaces;

namespace CoreKit.Identity.Tests.Support;

/// <summary>
/// Wraps a real <see cref="IUserRepository"/> and runs hooks immediately before selected
/// operations. A test uses this to park a request at the exact point where the old implementation
/// relied on a stale snapshot, let a concurrent request change the credential state, and then
/// release the transition — a deterministic race ordering with no timing-based sleeps.
/// </summary>
internal sealed class GatedUserRepository : IUserRepository
{
    private readonly IUserRepository _inner;
    private readonly Func<Task>? _beforeSuccessTransition;
    private readonly Func<Task>? _beforePasswordRehash;
    private readonly Func<Task>? _beforeCredentialUpdate;

    public GatedUserRepository(
        IUserRepository inner,
        Func<Task>? beforeSuccessTransition = null,
        Func<Task>? beforePasswordRehash = null,
        Func<Task>? beforeCredentialUpdate = null)
    {
        _inner = inner;
        _beforeSuccessTransition = beforeSuccessTransition;
        _beforePasswordRehash = beforePasswordRehash;
        _beforeCredentialUpdate = beforeCredentialUpdate;
    }

    public async Task<LoginCompletion> TryCompleteSuccessfulLoginAsync(
        Guid userId, int expectedSecurityVersion, DateTime now, CancellationToken ct = default)
    {
        if (_beforeSuccessTransition is not null)
            await _beforeSuccessTransition();
        return await _inner.TryCompleteSuccessfulLoginAsync(userId, expectedSecurityVersion, now, ct);
    }

    public async Task<bool> TryRehashPasswordAsync(
        Guid userId, string expectedCurrentHash, int expectedSecurityVersion, string newHash, DateTime now,
        CancellationToken ct = default)
    {
        if (_beforePasswordRehash is not null)
            await _beforePasswordRehash();
        return await _inner.TryRehashPasswordAsync(
            userId, expectedCurrentHash, expectedSecurityVersion, newHash, now, ct);
    }

    public Task<bool> RegisterFailedLoginAttemptAsync(
        Guid userId, DateTime now, DateTime lockoutEnd, int threshold, CancellationToken ct = default)
        => _inner.RegisterFailedLoginAttemptAsync(userId, now, lockoutEnd, threshold, ct);

    public async Task<CredentialUpdateResult> TryChangePasswordAsync(
        Guid userId,
        string expectedCurrentHash,
        int expectedSecurityVersion,
        string newHash,
        DateTime now,
        bool bumpSecurityVersion,
        bool revokeSessions,
        CancellationToken ct = default)
    {
        if (_beforeCredentialUpdate is not null)
            await _beforeCredentialUpdate();
        return await _inner.TryChangePasswordAsync(
            userId, expectedCurrentHash, expectedSecurityVersion, newHash, now,
            bumpSecurityVersion, revokeSessions, ct);
    }

    public Task<CredentialUpdateResult> TryDeactivateAsync(
        Guid userId, int expectedSecurityVersion, DateTime now, CancellationToken ct = default)
        => _inner.TryDeactivateAsync(userId, expectedSecurityVersion, now, ct);

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

    public Task ReplaceRolesAsync(User user, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default)
        => _inner.ReplaceRolesAsync(user, roleIds, ct);

    public Task DeleteAsync(User user, CancellationToken ct = default)
        => _inner.DeleteAsync(user, ct);
}