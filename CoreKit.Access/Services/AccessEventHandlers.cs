namespace CoreKit.Access.Services;

/// <summary>
/// Access no longer handles tenant lifecycle events directly. Tenant module registers its own
/// handlers (in Tenant project) that call IAccessMaintenance. Access keeps its own integration
/// event abstractions (IAccessIntegrationEvent, IAccessEventHandler<T>) for any module to use.
/// </summary>
