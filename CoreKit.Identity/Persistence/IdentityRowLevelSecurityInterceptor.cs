using CoreKit.Identity.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using System.Data.Common;

namespace CoreKit.Identity.Persistence;

/// <summary>
/// DbCommandInterceptor that sets PostgreSQL row-level security context before each command.
/// The user ID is resolved from <see cref="ICurrentUserService"/> and set as a session-level
/// configuration parameter (`app.user_id`). RLS policies on Identity user-owned tables filter rows
/// by this value, providing defense-in-depth even if an EF query filter is forgotten.
/// </summary>
public sealed class IdentityRowLevelSecurityInterceptor : DbCommandInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public IdentityRowLevelSecurityInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        SetUserContext(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        SetUserContext(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        SetUserContext(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetUserContext(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void SetUserContext(DbCommand command)
    {
        if (_currentUser.IsAuthenticated && _currentUser.UserId is { } userId)
        {
            var param = new NpgsqlParameter("app_user_id", userId.ToString());
            command.Parameters.Add(param);
            command.CommandText = $"SET LOCAL app.user_id = @app_user_id; " + command.CommandText;
        }
    }
}