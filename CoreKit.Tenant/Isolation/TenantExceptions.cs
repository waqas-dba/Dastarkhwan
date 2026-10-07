namespace CoreKit.Tenant.Isolation;

/// <summary>A tenant was required but none was resolved.</summary>
public sealed class TenantNotResolvedException : InvalidOperationException
{
    public TenantNotResolvedException()
        : base("No tenant has been resolved for this operation.")
    {
    }
}

/// <summary>Code tried to read or write across the tenant boundary.</summary>
public sealed class TenantIsolationException : InvalidOperationException
{
    public TenantIsolationException(string message) : base(message)
    {
    }
}