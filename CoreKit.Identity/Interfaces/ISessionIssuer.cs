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
    /// <param name="user">The user must be loaded with UserRoles and each UserRole's Role.</param>
    /// <param name="expectedSecurityVersion">The version observed together with the verified password hash.</param>
    Task<IdentityResult<LoginResponse>> IssueForLoginAsync(
        User user, int expectedSecurityVersion, CancellationToken ct = default);

    /// <summary>
    /// Issues a session for a valid refresh token. Same guarded insertion as
    /// <see cref="IssueForLoginAsync"/>, but an active login lockout does not block refresh:
    /// only credential-version and account-state guards apply.
    /// </summary>
    /// <param name="user">The user must be loaded with UserRoles and each UserRole's Role.</param>
    /// <param name="expectedSecurityVersion">The version observed when the refresh token was read.</param>
    Task<IdentityResult<LoginResponse>> IssueForRefreshAsync(
        User user, int expectedSecurityVersion, CancellationToken ct = default);
}