using System.Text.RegularExpressions;
using Iam.Core.Results;

namespace Iam.Core.Entities;

/// <summary>
/// One row per app that signs users in: "web-admin", "desktop-pos", "mobile-waiter".
/// The row holds the policy for that app, so web and desktop can behave differently
/// while sharing one IAM.
/// </summary>
public sealed partial class ClientApplication
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{2,63}$")]
    private static partial Regex ClientIdPattern();

    private ClientApplication() { } // used by EF Core

    public Guid Id { get; private set; }
    public string ClientId { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public ClientType Type { get; private set; }

    public int AccessTokenLifetimeMinutes { get; private set; }
    public int SessionLifetimeDays { get; private set; }

    /// <summary>When true, every refresh pushes the session end date forward.</summary>
    public bool UseSlidingSession { get; private set; }

    /// <summary>How many devices one user may be signed in on. Null means no limit.</summary>
    public int? MaxSessionsPerUser { get; private set; }

    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public static Result<ClientApplication> Create(
        string clientId, string name, ClientType type, DateTimeOffset now)
    {
        var id = clientId?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(id) || !ClientIdPattern().IsMatch(id))
            return Result.Failure<ClientApplication>(IamErrors.Clients.InvalidClientId);

        var displayName = name?.Trim();

        if (string.IsNullOrEmpty(displayName) || displayName.Length > 200)
            return Result.Failure<ClientApplication>(IamErrors.Clients.InvalidName);

        var defaults = DefaultsFor(type);

        return Result.Success(new ClientApplication
        {
            Id = Guid.NewGuid(),
            ClientId = id,
            Name = displayName,
            Type = type,
            AccessTokenLifetimeMinutes = defaults.AccessMinutes,
            SessionLifetimeDays = defaults.SessionDays,
            UseSlidingSession = defaults.Sliding,
            MaxSessionsPerUser = defaults.MaxSessions,
            IsActive = true,
            CreatedAtUtc = now
        });
    }

    public Result UpdatePolicy(
        int accessTokenMinutes, int sessionDays, bool sliding, int? maxSessionsPerUser, DateTimeOffset now)
    {
        if (accessTokenMinutes is < 1 or > 60 ||
            sessionDays is < 1 or > 365 ||
            maxSessionsPerUser is < 1)
            return Result.Failure(IamErrors.Clients.InvalidPolicy);

        AccessTokenLifetimeMinutes = accessTokenMinutes;
        SessionLifetimeDays = sessionDays;
        UseSlidingSession = sliding;
        MaxSessionsPerUser = maxSessionsPerUser;
        UpdatedAtUtc = now;
        return Result.Success();
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAtUtc = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAtUtc = now;
    }

    public TimeSpan GetAccessTokenLifetime() => TimeSpan.FromMinutes(AccessTokenLifetimeMinutes);
    public TimeSpan GetSessionLifetime() => TimeSpan.FromDays(SessionLifetimeDays);

    private static (int AccessMinutes, int SessionDays, bool Sliding, int? MaxSessions) DefaultsFor(ClientType type)
        => type switch
        {
            ClientType.Web => (10, 7, true, null),
            ClientType.Desktop => (15, 90, true, 5),
            ClientType.Mobile => (15, 90, true, 5),
            ClientType.Service => (60, 1, false, null),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
}