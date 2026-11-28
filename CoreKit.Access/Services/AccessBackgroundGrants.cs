using System.Security.Cryptography;
using System.Text.Json;
using CoreKit.Access.Abstractions;
using CoreKit.Access.Common;
using CoreKit.Access.Contracts;
using CoreKit.Access.Extensions;
using CoreKit.Access.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace CoreKit.Access.Services;

internal sealed class AccessBackgroundGrantProtector
{
    private const int CurrentVersion = 1;
    private const string Purpose = "CoreKit.Access.BackgroundGrant.v1";
    private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromSeconds(5);

    private readonly IDataProtector _protector;

    public AccessBackgroundGrantProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(Purpose);

    internal sealed record GrantPayload(
        int V, string JobKind, string RequiredPermission, Guid ActorUserId,
        Guid? TenantId, DateTime IssuedAt, DateTime ExpiresAt, Guid GrantId);

    internal sealed record Redeemed(
        AccessBackgroundExecution? Execution,
        AccessBackgroundRunStatus? Failure,
        Guid GrantId,
        DateTime ExpiresAt);

    public string Protect(
        string jobKind, string requiredPermission, Guid actorUserId, Guid? tenantId,
        DateTime issuedAt, DateTime expiresAt, Guid grantId)
        => _protector.Protect(JsonSerializer.Serialize(new GrantPayload(
            CurrentVersion, jobKind, requiredPermission, actorUserId,
            tenantId, issuedAt, expiresAt, grantId)));

    public Redeemed Redeem(
        string? token, string expectedJobKind, string expectedPermission, DateTime now)
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

        if (!string.Equals(payload.JobKind, expectedJobKind, StringComparison.Ordinal)
            || !string.Equals(payload.RequiredPermission, expectedPermission, StringComparison.Ordinal))
            return Fail(AccessBackgroundRunStatus.GrantMismatch);

        if (now >= payload.ExpiresAt)
            return Fail(AccessBackgroundRunStatus.GrantExpired);

        var tenantScope = payload.TenantId is { } tid ? AccessScope.ForTenant(tid) : (AccessScope?)null;

        return new Redeemed(
            new AccessBackgroundExecution(
                payload.JobKind,
                AccessBackgroundActorKind.User,
                payload.ActorUserId,
                tenantScope),
            Failure: null,
            GrantId: payload.GrantId,
            ExpiresAt: payload.ExpiresAt);
    }

    private static Redeemed Fail(AccessBackgroundRunStatus status)
        => new(null, status, Guid.Empty, default);
}

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
        string jobKind, string requiredPermission, TimeSpan? lifetime = null,
        CancellationToken ct = default)
    {
        if (_holder.IsActive) return AccessErrors.Denied;

        if (!AccessBackgroundRules.IsValidJobKind(jobKind) || string.IsNullOrWhiteSpace(requiredPermission))
            return AccessErrors.Validation("A job kind and a required permission are needed.");

        var options = _options.Value;
        var life = lifetime ?? options.DefaultGrantLifetime;
        if (life <= TimeSpan.Zero || life > options.MaxGrantLifetime)
            return AccessErrors.Validation("The grant lifetime is outside the allowed range.");

        if (!_actor.IsAuthenticated || _actor.UserId is not { } userId || userId == Guid.Empty)
            return AccessErrors.NotAuthenticated;

        var scope = _scopeContext.Current;
        if (scope is { } present && !present.IsValid)
            return AccessErrors.Denied;

        var checkScope = scope ?? AccessScope.ForPlatform(AccessScope.PlatformId);
        var access = await _access.GetFreshAccessAsync(userId, checkScope, ct);
        if (!access.Has(requiredPermission)) return AccessErrors.Denied;

        var tenantId = checkScope.Type == AccessScope.TenantType ? checkScope.Id : (Guid?)null;
        var now = _time.GetUtcNow().UtcDateTime;
        var grantId = Guid.NewGuid();

        var token = _protector.Protect(jobKind, requiredPermission, userId, tenantId,
            now, now + life, grantId);

        return AccessResult.Success(new AccessBackgroundGrantToken(token));
    }
}