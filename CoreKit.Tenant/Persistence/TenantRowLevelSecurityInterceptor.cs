using CoreKit.Tenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using System.Data.Common;

namespace CoreKit.Tenant.Persistence;

/// <summary>
/// DbCommandInterceptor that sets PostgreSQL row-level security context before each command.
/// The tenant ID is resolved from <see cref="ICurrentTenant"/> and set as a session-level
/// configuration parameter (`app.tenant_id`). RLS policies on tenant tables filter rows
/// by this value, providing defense-in-depth even if an EF query filter is forgotten.
/// </summary>
public sealed class TenantRowLevelSecurityInterceptor : DbCommandInterceptor
{
    private readonly ICurrentTenant _currentTenant;

    public TenantRowLevelSecurityInterceptor(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        SetTenantContext(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        SetTenantContext(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        SetTenantContext(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetTenantContext(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void SetTenantContext(DbCommand command)
    {
        if (_currentTenant.IsResolved && _currentTenant.Id is { } tenantId)
        {
            var param = new NpgsqlParameter("app_tenant_id", tenantId.ToString());
            command.Parameters.Add(param);
            command.CommandText = $"SET LOCAL app.tenant_id = @app_tenant_id; " + command.CommandText;
        }
    }
}
