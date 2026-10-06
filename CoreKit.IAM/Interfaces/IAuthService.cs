
using CoreKit.IAM.Models;
using CoreKit.IAM.Common;

namespace CoreKit.IAM.Interfaces;

public interface IAuthService
{
    Task<IamResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);

    Task<IamResult<LoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);

    Task<IamResult> LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default);

    Task<IamResult<UserDto>> GetCurrentUserAsync(CancellationToken ct = default);

    /// <summary>Changes the signed-in user's own password and ends all of their sessions.</summary>
    Task<IamResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
}