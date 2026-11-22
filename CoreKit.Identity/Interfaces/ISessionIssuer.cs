using CoreKit.Identity.Common;
using CoreKit.Identity.Entities;
using CoreKit.Identity.Models;

namespace CoreKit.Identity.Interfaces;

/// <summary>Creates the access token and a new stored refresh token for a user who has just been authenticated.</summary>
public interface ISessionIssuer
{
    /// <summary>
    /// Issues a session for a user who just completed the authoritative login transition.
    /// The refresh-token insertion is guarded in the same transaction as the user-row lock: it
    /// is refused when the account is inactive, its security version moved on, or it became
    /// locked out after the login transition.
    /// </summary>
    /// <param name="user">The authenticated user entity.</param>
    /// <param name="expectedSecurityVersion">The version observed together with the verified password hash.</param>
    Task<IdentityResult<LoginResponse>> IssueForLoginAsync(
        User user, int expectedSecurityVersion, CancellationToken ct = default);

    /// <summary>
    /// Issues a session for a valid refresh token. Same guarded insertion as
    /// <see cref="IssueForLoginAsync"/>, but an active login lockout does not block refresh:
    /// only credential-version and account-state guards apply.
    /// </summary>
    /// <param name="user">The authenticated user entity.</param>
    /// <param name="expectedSecurityVersion">The version observed when the refresh token was read.</param>
    /// <param name="familyId">The family ID of the token being refreshed. All tokens in a rotation chain share the same family ID.</param>
    Task<IdentityResult<LoginResponse>> IssueForRefreshAsync(
        User user, int expectedSecurityVersion, Guid familyId, CancellationToken ct = default);

    /// <summary>
    /// Atomically rotates a refresh token: revokes the old token and issues a new one in a single
    /// transaction with user-row guard. This prevents the window between revoke and issue where
    /// a concurrent request could steal the token.
    /// </summary>
    /// <param name="user">The authenticated user entity.</param>
    /// <param name="expectedSecurityVersion">The version observed when the refresh token was read.</param>
    /// <param name="oldTokenId">The ID of the token being rotated (consumed).</param>
    /// <param name="familyId">The family ID of the token being refreshed. All tokens in a rotation chain share the same family ID.</param>
    Task<IdentityResult<LoginResponse>> RotateAsync(
        User user, int expectedSecurityVersion, Guid oldTokenId, Guid familyId, CancellationToken ct = default);
}