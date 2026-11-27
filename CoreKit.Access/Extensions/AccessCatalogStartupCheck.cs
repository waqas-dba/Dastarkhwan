using CoreKit.Access.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoreKit.Access.Extensions;

internal sealed class AccessCatalogStartupCheck : IHostedService
{
    private readonly IServiceProvider _root;
    public AccessCatalogStartupCheck(IServiceProvider root) => _root = root;

    public Task StartAsync(CancellationToken ct)
    {
        _ = _root.GetRequiredService<AccessCatalog>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}