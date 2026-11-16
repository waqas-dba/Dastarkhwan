using System.Security.Cryptography;
using System.Text.Json;
using CoreKit.Access.Common;
using CoreKit.Access.Extensions;
using Microsoft.AspNetCore.DataProtection;

namespace CoreKit.Access.Services;

/// <summary>
/// Protects and redeems background grants. The grant is a Data Protection payload: it is encrypted and
/// authenticated with the application's key ring, so a forged or edited value is rejected instead of trusted.
/// Hand-rolled signing is deliberately avoided.
/// </summary>
internal sealed class AccessBackgroundGrantProtector
{
    private const int CurrentVersion = 1;
    private const string Purpose = "CoreKit.Access.BackgroundGrant.v1";

    // A grant from a clock that runs a few seconds ahead is accepted. Anything further in the future is invalid.
    private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromSeconds(5);

    private readonly IDataProtector _protector;

    public AccessBackgroundGrantProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(Purpose);

    /// <summary>
    /// The encrypted body. The job kind and the required permission are part of the signed payload, so the
    /// runner cannot redeem a grant for a different job or a different permission than the one authorized.
    /// </summary>
    internal sealed record GrantPayload(
        int V,
        string JobKind,
        string RequiredPermission,
        Guid ActorUserId,
        Guid? TenantId,
        DateTime IssuedAt,
        DateTime ExpiresAt,
        Guid GrantId);

    internal sealed record Redeemed(AccessBackgroundExecution? Execution, AccessBackgroundRunStatus? Failure);

    public string Protect(
        string jobKind,
        string requiredPermission,
        Guid actorUserId,
        Guid? tenantId,
        DateTime issuedAt,
        DateTime expiresAt)
        => _protector.Protect(JsonSerializer.Serialize(new GrantPayload(
            CurrentVersion,
            jobKind,
            requiredPermission,
            actorUserId,
            tenantId,
            issuedAt,
            expiresAt,
            Guid.NewGuid())));

    /// <summary>
    /// Validates the token and rebuilds the execution context. Every failure is a value, never an exception,
    /// so a job runner can record why a job was refused.
    /// </summary>
    public Redeemed Redeem(string? token, string expectedJobKind, string expectedPermission, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(token)
            || !AccessBackgroundRules.IsValidJobKind(expectedJobKind)
            || string.IsNullOrWhiteSpace(expectedPermission))
            return Fail(AccessBackgroundRunStatus.GrantInvalid);

        GrantPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<GrantPayload>(_protector.Unprotect(token));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return Fail(AccessBackgroundRunStatus.GrantInvalid);
        }

        if (payload is null
            || payload.V != CurrentVersion
            || !AccessBackgroundRules.IsValidJobKind(payload.JobKind)
            || string.IsNullOrWhiteSpace(payload.RequiredPermission)
            || payload.ActorUserId == Guid.Empty
            || payload.GrantId == Guid.Empty
            || payload.TenantId == Guid.Empty
            || payload.ExpiresAt <= payload.IssuedAt
            || payload.IssuedAt > now + MaxFutureSkew)
            return Fail(AccessBackgroundRunStatus.GrantInvalid);

        // The runner must ask for exactly the job and the permission that were authorized. This stops a runner
        // from reusing a grant for another permission the actor happens to hold.
        if (!string.Equals(payload.JobKind, expectedJobKind, StringComparison.Ordinal)
            || !string.Equals(payload.RequiredPermission, expectedPermission, StringComparison.Ordinal))
            return Fail(AccessBackgroundRunStatus.GrantMismatch);

        if (now >= payload.ExpiresAt)
            return Fail(AccessBackgroundRunStatus.GrantExpired);

        return new Redeemed(
            new AccessBackgroundExecution(
                payload.JobKind,
                AccessBackgroundActorKind.User,
                payload.ActorUserId,
                payload.TenantId is { } tenantId ? AccessScope.ForTenant(tenantId) : null),
            null);
    }

    private static Redeemed Fail(AccessBackgroundRunStatus status) => new(null, status);
}

/// <summary>
/// Issues grants from an authorized request. The scope and actor come from the request context, and the
/// permission is checked on fresh data, so a person whose access was just withdrawn cannot mint a grant.
/// </summary>
internal sealed class AccessBackgroundGrantIssuer : IAccessBackgroundGrantIssuer
{
    private readonly IAccessService _access;
    private readonly IAccessScopeContext _scopeContext;
    private readonly IAccessCurrentUser _actor;
    private readonly AccessBackgroundContextHolder _holder;
    private readonly AccessBackgroundGrantProtector _protector;
    private readonly IOptions<AccessBackgroundOptions> _options;
    private readonly TimeProvider _time;

    public AccessBackgroundGrantIssuer(
        IAccessService access,
        IAccessScopeContext scopeContext,
        IAccessCurrentUser actor,
        AccessBackgroundContextHolder holder,
        AccessBackgroundGrantProtector protector,
        IOptions<AccessBackgroundOptions> options,
        TimeProvider time)
    {
        _access = access;
        _scopeContext = scopeContext;
        _actor = actor;
        _holder = holder;
        _protector = protector;
        _options = options;
        _time = time;
    }

    public async Task<AccessResult<AccessBackgroundGrantToken>> IssueAsync(
        string jobKind, string requiredPermission, TimeSpan? lifetime = null, CancellationToken ct = default)
    {
        // Authority is never re-issued from inside a running job. A job chain must be re-authorized by a new
        // request, or run as a trusted system job.
        if (_holder.IsActive)
            return AccessErrors.Denied;

        if (!AccessBackgroundRules.IsValidJobKind(jobKind) || string.IsNullOrWhiteSpace(requiredPermission))
            return AccessErrors.Validation("A job kind and a required permission are needed.");

        var options = _options.Value;
        var life = lifetime ?? options.DefaultGrantLifetime;
        if (life <= TimeSpan.Zero || life > options.MaxGrantLifetime)
            return AccessErrors.Validation("The grant lifetime is outside the allowed range.");

        if (!_actor.IsAuthenticated || _actor.UserId is not { } userId || userId == Guid.Empty)
            return AccessErrors.NotAuthenticated;

        // A grant delegates authority, so it is decided on fresh data, never on a cached result.
        if (_access is not IFreshAccessSource fresh)
            return AccessErrors.Denied;

        var scope = _scopeContext.Current;
        if (scope is { } present && (!present.IsValid || present.Type != AccessScope.TenantType))
            return AccessErrors.Denied;

        // No tenant scope means a platform-level job, authorized against the fixed platform scope.
        var checkScope = scope ?? AccessScope.ForPlatform(AccessScope.PlatformId);

        var access = await fresh.GetFreshAccessAsync(userId, checkScope, ct);
        if (!access.Has(requiredPermission))
            return AccessErrors.Denied;

        var now = _time.GetUtcNow().UtcDateTime;
        var token = _protector.Protect(jobKind, requiredPermission, userId, scope?.Id, now, now + life);

        return AccessResult.Success(new AccessBackgroundGrantToken(token));
    }
}