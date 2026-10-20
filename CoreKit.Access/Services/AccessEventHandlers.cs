namespace CoreKit.Access.Services;

/// <summary>A new tenant starts with the role templates the application declared.</summary>
internal sealed class TenantCreatedHandler : IEventHandler<TenantCreated>
{
    private readonly IAccessMaintenance _access;

    public TenantCreatedHandler(IAccessMaintenance access) => _access = access;

    public Task HandleAsync(TenantCreated integrationEvent, CancellationToken ct = default)
        => _access.ProvisionScopeAsync(AccessScope.ForTenant(integrationEvent.TenantId), ct);
}

/// <summary>Someone who leaves a tenant loses their roles in it.</summary>
internal sealed class TenantMemberRemovedHandler : IEventHandler<TenantMemberRemoved>
{
    private readonly IAccessMaintenance _access;

    public TenantMemberRemovedHandler(IAccessMaintenance access) => _access = access;

    public Task HandleAsync(TenantMemberRemoved integrationEvent, CancellationToken ct = default)
        => _access.RemoveUserAsync(AccessScope.ForTenant(integrationEvent.TenantId), integrationEvent.UserId, ct);
}

/// <summary>A deleted tenant leaves no roles behind.</summary>
internal sealed class TenantDeletedHandler : IEventHandler<TenantDeleted>
{
    private readonly IAccessMaintenance _access;

    public TenantDeletedHandler(IAccessMaintenance access) => _access = access;

    public Task HandleAsync(TenantDeleted integrationEvent, CancellationToken ct = default)
        => _access.RemoveScopeAsync(AccessScope.ForTenant(integrationEvent.TenantId), ct);
}