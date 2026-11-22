namespace CoreKit.Access.Abstractions;

/// <summary>
/// Distributed cache invalidation for multi-instance deployments.
/// When a role or assignment changes on one instance, it publishes an invalidation event
/// that all instances receive and apply to their local caches.
/// </summary>
public interface IDistributedCacheInvalidator
{
    /// <summary>Publishes a scope invalidation to all instances.</summary>
    Task InvalidateScopeAsync(AccessScope scope, CancellationToken ct = default);

    /// <summary>Publishes a user invalidation to all instances.</summary>
    Task InvalidateUserAsync(AccessScope scope, Guid userId, CancellationToken ct = default);

    /// <summary>Publishes a SecurityVersion invalidation (for Identity tokens).</summary>
    Task InvalidateSecurityVersionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Subscribes to invalidation events. Called once at startup.</summary>
    void Subscribe(Action<InvalidationMessage> handler);
}

/// <summary>Message received from the distributed invalidation channel.</summary>
public sealed record InvalidationMessage(
    InvalidationType Type,
    AccessScope? Scope = null,
    Guid? UserId = null);

/// <summary>Type of invalidation.</summary>
public enum InvalidationType
{
    Scope = 0,
    User = 1,
    SecurityVersion = 2
}