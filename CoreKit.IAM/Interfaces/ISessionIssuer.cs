using CoreKit.IAM.Entities;
using CoreKit.IAM.Models;

namespace CoreKit.IAM.Interfaces;

/// <summary>Creates the access token and a new stored refresh token for a user who has just been authenticated.</summary>
public interface ISessionIssuer
{
    /// <summary>The user must be loaded with UserRoles and each UserRole's Role.</summary>
    Task<LoginResponse> IssueAsync(User user, CancellationToken ct = default);
}