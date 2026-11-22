namespace CoreKit.Access.Abstractions;

/// <summary>
/// Trusted one-time platform provisioning: bootstraps the initial platform administrator.
///
/// This is intentionally NOT part of the public <see cref="IAccessMaintenance"/> contract and is NOT
/// registered in the general application DI container. Ordinary application callers, controllers and
/// unauthenticated HTTP requests cannot resolve or invoke it. A trusted deployment path (for example an
/// administrative command running under OS/deployment authority) is the only intended invoker, and that
/// composition remains a separate concern.
///
/// The bootstrap operation records a System audit actor whose source identifies the trusted deployment
/// origin. The target user id is never treated as the operator, and a caller-supplied source string alone
/// cannot establish trusted system identity.
/// </summary>
public interface IAccessBootstrap
{
    /// <summary>
    /// Bootstraps the initial platform administrator (idempotent, trusted deployment operation).
    /// Creates/activates platform membership and adds the Administrator role without removing
    /// unrelated assignments. Refuses if an active platform administrator already exists for
    /// a different user. Trusted system provisioning — no permission check.
    /// </summary>
    /// <param name="userId">The target user to make the initial platform administrator.</param>
    /// <param name="ct">Cancellation token.</param>
    Task BootstrapInitialPlatformAdminAsync(Guid userId, CancellationToken ct = default);
}
