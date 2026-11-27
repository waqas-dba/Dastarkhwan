using Microsoft.Extensions.Hosting;

namespace CoreKit.Access.Extensions;

/// <summary>
/// How long a background grant stays valid. Keep it short: a grant can be replayed until it expires, and the
/// job store is responsible for making a job run exactly once.
/// </summary>
public sealed class AccessBackgroundOptions
{
    /// <summary>Lifetime of a grant when the caller does not choose one. Defaults to 5 minutes.</summary>
    public TimeSpan DefaultGrantLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The longest a grant may live. Defaults to 30 minutes. Requests above this are refused.
    /// </summary>
    public TimeSpan MaxGrantLifetime { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Opt-in wiring for background execution. It wraps the request-scoped Access context so that a background
/// job can carry an explicit, trusted scope and actor without touching HttpContext.
/// </summary>
public static class AccessBackgroundContextExtensions
{
    /// <summary>
    /// Adds the explicit background execution context.
    ///
    /// Call it AFTER <c>AddAccess</c> and after the identity module has registered its
    /// <c>IAccessCurrentUser</c>, because it wraps the registrations that exist at that moment. A startup check
    /// fails fast if a later registration replaced a wrapper.
    ///
    /// Multi-instance hosts must share the Data Protection key ring (for example
    /// <c>services.AddDataProtection().PersistKeysTo...</c>). Otherwise a grant issued by one instance is not
    /// readable by another and is refused as invalid (fail closed).
    /// </summary>
    public static IServiceCollection AddAccessBackgroundContext(
        this IServiceCollection services, Action<AccessBackgroundOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(d => d.ServiceType == typeof(IAccessBackgroundContext)))
            throw new InvalidOperationException(
                "AddAccessBackgroundContext is already registered. Call it once, after AddAccess and the identity module.");

        Decorate<IAccessScopeContext>(services, (inner, sp) =>
            new BackgroundAwareScopeContext(inner, sp.GetRequiredService<AccessBackgroundContextHolder>()));

        Decorate<IAccessCurrentUser>(services, (inner, sp) =>
            new BackgroundAwareCurrentUser(inner, sp.GetRequiredService<AccessBackgroundContextHolder>()));

        var builder = services.AddOptions<AccessBackgroundOptions>();
        if (configure is not null)
            builder.Configure(configure);

        builder.Validate(
            o => o.DefaultGrantLifetime > TimeSpan.Zero
                 && o.MaxGrantLifetime >= o.DefaultGrantLifetime
                 && o.MaxGrantLifetime <= TimeSpan.FromMinutes(30),
            "Access background grant lifetimes are invalid (default > 0, default <= max <= 30m).");

        services.AddDataProtection();
        services.TryAddSingleton(TimeProvider.System);

        services.TryAddScoped<AccessBackgroundContextHolder>();
        services.TryAddScoped<IAccessBackgroundContext>(sp => sp.GetRequiredService<AccessBackgroundContextHolder>());
        services.TryAddSingleton<AccessBackgroundGrantProtector>();
        services.TryAddScoped<IAccessBackgroundGrantIssuer, AccessBackgroundGrantIssuer>();
        services.TryAddSingleton<IAccessBackgroundExecutor, AccessBackgroundExecutor>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, AccessBackgroundContextStartupCheck>());

        return services;
    }

    /// <summary>
    /// Replaces the last registration of <typeparamref name="TService"/> with a wrapper that adds the
    /// background context. The last registration is the one that currently wins, so earlier registrations keep
    /// their meaning and the wrapper stays in the same lifetime.
    /// </summary>
    private static void Decorate<TService>(IServiceCollection services, Func<TService, IServiceProvider, TService> wrap)
        where TService : class
    {
        var index = -1;
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(TService))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
            throw new InvalidOperationException(
                $"{typeof(TService).Name} is not registered. Call AddAccessBackgroundContext after AddAccess and the identity module.");

        var inner = services[index];

        // The wrapper resolves the scoped holder, so a singleton (or transient) registration would capture it
        // for the wrong lifetime. Fail with a clear message instead of a captive-dependency error later.
        if (inner.Lifetime != ServiceLifetime.Scoped)
            throw new InvalidOperationException(
                $"{typeof(TService).Name} must be registered as scoped for the background context wrapper.");

        services[index] = ServiceDescriptor.Describe(
            typeof(TService),
            sp => wrap(CreateInner<TService>(sp, inner), sp),
            inner.Lifetime);
    }

    private static TService CreateInner<TService>(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is TService instance)
            return instance;

        if (descriptor.ImplementationFactory is not null)
            return (TService)descriptor.ImplementationFactory(sp);

        if (descriptor.ImplementationType is not null)
            return (TService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);

        throw new InvalidOperationException(
            $"The existing {typeof(TService).Name} registration has no implementation to wrap.");
    }
}

/// <summary>
/// Fails the host at startup if another registration replaced a background-aware wrapper. Without this check,
/// a silently replaced wrapper would make background code look unauthenticated instead of reporting the wiring
/// mistake.
/// </summary>
internal sealed class AccessBackgroundContextStartupCheck : IHostedService
{
    private readonly IServiceProvider _root;

    public AccessBackgroundContextStartupCheck(IServiceProvider root) => _root = root;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Verify(_root);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static void Verify(IServiceProvider root)
    {
        using var scope = root.CreateScope();

        if (scope.ServiceProvider.GetRequiredService<IAccessScopeContext>() is not BackgroundAwareScopeContext
            || scope.ServiceProvider.GetRequiredService<IAccessCurrentUser>() is not BackgroundAwareCurrentUser)
        {
            throw new InvalidOperationException(
                "The background-aware Access context was replaced by a later registration. " +
                "Call AddAccessBackgroundContext as the last Access-related registration step, " +
                "after every registration of IAccessScopeContext and IAccessCurrentUser.");
        }
    }
}