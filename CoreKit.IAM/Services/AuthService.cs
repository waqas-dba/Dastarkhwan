
using CoreKit.IAM.Common;
using CoreKit.IAM.Interfaces;
using CoreKit.IAM.Models;
using CoreKit.IAM.Persistence;
using CoreKit.IAM.Security;
using Microsoft.Extensions.Logging;
using System.Text.Json;


namespace CoreKit.IAM.Services;


public sealed class AuthService : IAuthService
{
    // Longer input is never a real login, and hashing huge strings would be an easy way to slow the server down.
    private const int MaxEmailLength = 320;

    private readonly IamDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _time;
    private readonly ILogger<AuthService> _logger;
    private readonly IEnumerable<IAccessTokenClaimsContributor> _contributors;

    private readonly IamEmailVerificationOptions _emailOptions;

    public AuthService(
        IamDbContext db,
        IPasswordHasher hasher,
        ITokenService tokens,
        ICurrentUserService currentUser,
        IEnumerable<IAccessTokenClaimsContributor> contributors,
        IOptions<IamEmailVerificationOptions> emailOptions,
        TimeProvider time,
        ILogger<AuthService> logger)
   
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _currentUser = currentUser;
        _contributors = contributors;
        _time = time;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<IamResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (email.Length == 0 || email.Length > MaxEmailLength ||
            password.Length == 0 || password.Length > PasswordPolicy.MaxLength)
            return IamErrors.InvalidCredentials;

        var normalizedEmail = IamNormalizer.NormalizeEmail(email);

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (user is null)
        {
            // Spend the same time as a real check, so response time does not reveal which emails exist.
            _ = _hasher.Hash(password);
            _logger.LogInformation("Login failed.");
            return IamErrors.InvalidCredentials;
        }

        var check = _hasher.Verify(user.PasswordHash, password);

        if (check == PasswordCheckResult.Failed || !user.IsActive)
        {
            _logger.LogInformation("Login failed for user {UserId}.", user.Id);
            return IamErrors.InvalidCredentials;
        }

        // Only reached with the right password, so this message cannot be used to discover accounts.
        if (_emailOptions.RequireConfirmedEmail && !user.EmailConfirmed)
        {
            _logger.LogInformation("Login blocked for user {UserId}: email not confirmed.", user.Id);
            return IamEmailErrors.EmailNotConfirmed;
        }

        if (check == PasswordCheckResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.Hash(password);
            user.UpdatedAt = Now();
        }

        return IamResult.Success(await IssueTokensAsync(user, ct));
    }

    public async Task<IamResult<LoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var rawToken = request.RefreshToken;

        if (string.IsNullOrWhiteSpace(rawToken))
            return IamErrors.InvalidRefreshToken;

        var tokenHash = _tokens.HashRefreshToken(rawToken.Trim());

        var token = await _db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (token is null)
            return IamErrors.InvalidRefreshToken;

        var now = Now();

        if (token.RevokedAt is not null)
        {
            // A token that was already used or revoked has come back. Whoever holds it may have copied it,
            // so every session of this user is closed.
            _logger.LogWarning(
                "A revoked refresh token was presented for user {UserId}. Revoking all of the user's refresh tokens.",
                token.UserId);

            await RefreshTokenRevocation.RevokeAllForUserAsync(_db, token.UserId, now, ct);
            return IamErrors.InvalidRefreshToken;
        }

        if (token.ExpiresAt <= now)
            return IamErrors.InvalidRefreshToken;

        if (!token.User.IsActive)
        {
            await RefreshTokenRevocation.RevokeAllForUserAsync(_db, token.UserId, now, ct);
            return IamErrors.InvalidRefreshToken;
        }

        if (_emailOptions.RequireConfirmedEmail && !token.User.EmailConfirmed)
        {
            await RefreshTokenRevocation.RevokeAllForUserAsync(_db, token.UserId, now, ct);
            return IamEmailErrors.EmailNotConfirmed;
        }

        // Atomic: of two requests using the same token at the same moment, only one changes a row.
        var revoked = await _db.RefreshTokens
            .Where(t => t.Id == token.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now), ct);

        if (revoked == 0)
            return IamErrors.InvalidRefreshToken;

        return IamResult.Success(await IssueTokensAsync(token.User, ct));
    }

    public async Task<IamResult> LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return IamResult.Success();

        var tokenHash = _tokens.HashRefreshToken(request.RefreshToken.Trim());
        var now = Now();

        // Signing out twice, or with an unknown token, is not an error.
        await _db.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now), ct);

        return IamResult.Success();
    }

    public async Task<IamResult<UserDto>> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId)
            return IamErrors.NotAuthenticated;

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null || !user.IsActive)
            return IamErrors.NotAuthenticated;

        var (_, permissions) = await IamMapper.ResolveAccessAsync(_db, user, ct);

        return IamResult.Success(IamMapper.ToDto(user, permissions));
    }

    public async Task<IamResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId)
            return IamErrors.NotAuthenticated;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null || !user.IsActive)
            return IamErrors.NotAuthenticated;

        var currentPassword = request.CurrentPassword ?? string.Empty;

        if (currentPassword.Length > PasswordPolicy.MaxLength ||
            _hasher.Verify(user.PasswordHash, currentPassword) == PasswordCheckResult.Failed)
            return IamErrors.WrongCurrentPassword;

        var policyError = PasswordPolicy.Validate(request.NewPassword, user.Email);

        if (policyError is not null)
            return IamErrors.Validation(policyError);

        if (request.NewPassword == currentPassword)
            return IamErrors.Validation("The new password must be different from the current one.");

        var now = Now();

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        // Every device must sign in again with the new password.
        await RefreshTokenRevocation.RevokeAllForUserAsync(_db, userId, now, ct);

        return IamResult.Success();
    }

    private async Task<LoginResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var (roles, permissions) = await IamMapper.ResolveAccessAsync(_db, user, ct);

        var extra = new List<Claim>();
        foreach (var contributor in _contributors)
            extra.AddRange(await contributor.GetClaimsAsync(user, ct));

        var accessToken = _tokens.CreateAccessToken(user, roles, permissions, extra);
        var refreshToken = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshToken.TokenHash,
            CreatedAt = Now(),
            ExpiresAt = refreshToken.ExpiresAt
        });

        await _db.SaveChangesAsync(ct);

        return new LoginResponse
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = refreshToken.RawToken,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
            User = IamMapper.ToDto(user, permissions)
        };
    }
    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}