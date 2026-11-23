using CoreKit.Identity.Common;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Interfaces;
using CoreKit.Identity.Models;

namespace CoreKit.Identity.Tests.Support;

/// <summary>
/// Wraps a real <see cref="ISessionIssuer"/> and runs a hook immediately before the guarded
/// issuance. A test uses this to park a login or refresh request at the exact point where a
/// concurrent credential change or deactivation must invalidate it — a deterministic race
/// ordering with no timing-based sleeps.
/// </summary>
internal sealed class GatedSessionIssuer : ISessionIssuer
{
    private readonly ISessionIssuer _inner;
    private readonly Func<Task> _beforeIssuance;

    public GatedSessionIssuer(ISessionIssuer inner, Func<Task> beforeIssuance)
    {
        _inner = inner;
        _beforeIssuance = beforeIssuance;
    }

    public async Task<IdentityResult<LoginResponse>> IssueForLoginAsync(
        User user, int expectedSecurityVersion, CancellationToken ct = default)
    {
        await _beforeIssuance();
        return await _inner.IssueForLoginAsync(user, expectedSecurityVersion, ct);
    }

    public async Task<IdentityResult<LoginResponse>> IssueForRefreshAsync(
        User user, int expectedSecurityVersion, Guid familyId, CancellationToken ct = default)
    {
        await _beforeIssuance();
        return await _inner.IssueForRefreshAsync(user, expectedSecurityVersion, familyId, ct);
    }

    public async Task<IdentityResult<LoginResponse>> RotateAsync(
        User user, int expectedSecurityVersion, Guid oldTokenId, Guid familyId, CancellationToken ct = default)
    {
        await _beforeIssuance();
        return await _inner.RotateAsync(user, expectedSecurityVersion, oldTokenId, familyId, ct);
    }
}