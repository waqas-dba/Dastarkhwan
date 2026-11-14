namespace CoreKit.Tenant.Services;

public sealed class TenantPlatformAccess : ITenantPlatformAccess
{
    private readonly ITenantActor _actor;
    private int _grants;

    public TenantPlatformAccess(ITenantActor actor) => _actor = actor;

    public Task<bool> IsGrantedAsync(CancellationToken ct = default)
        => _grants > 0 ? Task.FromResult(true) : _actor.IsPlatformAdminAsync(ct);

    public IDisposable Grant()
    {
        _grants++;
        return new GrantScope(this);
    }

    private void Release()
    {
        if (_grants > 0)
            _grants--;
    }

    private sealed class GrantScope : IDisposable
    {
        private readonly TenantPlatformAccess _owner;
        private bool _disposed;

        public GrantScope(TenantPlatformAccess owner) => _owner = owner;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _owner.Release();
        }
    }
}