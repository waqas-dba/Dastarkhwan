namespace CoreKit.Access.Abstractions;

using CoreKit.Access.Contracts;

/// <summary>
/// Where the current request happens. By default it is the current tenant (from the shared kernel's
/// ICurrentTenant), so roles belong to a tenant. Another module can supply its own, for example a store.
/// </summary>
public interface IAccessScopeContext
{
    AccessScope? Current { get; }
}