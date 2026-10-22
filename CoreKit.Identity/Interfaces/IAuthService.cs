
using CoreKit.Identity.Models;
using CoreKit.Identity.Common;

namespace CoreKit.Identity.Interfaces;

public interface IAuthService
{
    Task<IdentityResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);

    Task<IdentityResult<LoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);

    Task<Common.IdentityResult> LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default);

    Task<IdentityResult<UserDto>> GetCurrentUserAsync(CancellationToken ct = default);

    /// <summary>Changes the signed-in user's own password and ends all of their sessions.</summary>
    Task<Common.IdentityResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
}