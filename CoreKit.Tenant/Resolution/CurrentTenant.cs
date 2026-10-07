namespace CoreKit.Tenant.Resolution;

/// <summary>Lets the resolution middleware record which tenant a request belongs to.</summary>
internal interface ITenantContextSetter
{
    void Set(Guid tenantId);
}

/// <summary>Scoped: one instance per request, so tenants can never leak between requests.</summary>
internal sealed class CurrentTenant : ICurrentTenant, ITenantContextSetter
{
    private Guid? _id;

    public bool IsResolved => _id.HasValue;

    public Guid? Id => _id;

    public Guid RequiredId => _id ?? throw new TenantNotResolvedException();

    public void Set(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id cannot be empty.", nameof(tenantId));

        _id = tenantId;
    }

    public IDisposable Change(Guid? tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant id cannot be empty.", nameof(tenantId));

        var previous = _id;
        _id = tenantId;

        return new Restorer(() => _id = previous);
    }

    private sealed class Restorer : IDisposable
    {
        private readonly Action _restore;
        private bool _disposed;

        public Restorer(Action restore) => _restore = restore;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _restore();
        }
    }
}