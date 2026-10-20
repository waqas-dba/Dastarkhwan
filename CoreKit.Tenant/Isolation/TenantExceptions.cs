namespace CoreKit.Tenant.Isolation;

/// <summary>Code tried to read or write across the tenant boundary.</summary>
public sealed class TenantIsolationException : InvalidOperationException
{
    public TenantIsolationException(string message) : base(message)
    {
    }
}