using Npgsql;
using Xunit.Abstractions;

namespace CoreKit.Access.Tests.Support;

/// <summary>
/// An isolated PostgreSQL scratch database for integration tests. Creates a uniquely named database
/// on the server named by <c>ConnectionStrings__Access</c>, and drops it on disposal. The configured
/// database itself is never touched. When the environment variable is missing, or a scratch database
/// cannot be created, the test reports the reason through <see cref="ITestOutputHelper"/> and skips.
/// </summary>
internal sealed class PostgresScratchDb : IAsyncDisposable
{
    private readonly string _serverConnectionString;
    private readonly string _databaseName;

    private PostgresScratchDb(string serverConnectionString, string databaseName, string connectionString)
    {
        _serverConnectionString = serverConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<PostgresScratchDb?> TryCreateAsync(ITestOutputHelper output)
    {
        var baseConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Access");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            output.WriteLine("Skipped: ConnectionStrings__Access is not set; no PostgreSQL integration coverage in this environment.");
            return null;
        }

        var databaseName = "corekit_access_it_" + Guid.NewGuid().ToString("N")[..16];

        try
        {
            var serverBuilder = new NpgsqlConnectionStringBuilder(baseConnection)
            {
                Database = "postgres",
                Pooling = false
            };

            await using var connection = new NpgsqlConnection(serverBuilder.ConnectionString);
            await connection.OpenAsync();

            await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection))
                await create.ExecuteNonQueryAsync();

            return new PostgresScratchDb(
                serverBuilder.ConnectionString,
                databaseName,
                new NpgsqlConnectionStringBuilder(baseConnection) { Database = databaseName }.ConnectionString);
        }
        catch (Exception ex)
        {
            output.WriteLine($"Skipped: could not create an isolated scratch database on the configured PostgreSQL server ({ex.Message}).");
            await TryDropAsync(baseConnection, databaseName);
            return null;
        }
    }

    public async ValueTask DisposeAsync()
        => await TryDropAsync(_serverConnectionString, _databaseName);

    private static async Task TryDropAsync(string serverConnectionString, string databaseName)
    {
        try
        {
            await using var connection = new NpgsqlConnection(serverConnectionString);
            await connection.OpenAsync();

            await using (var terminate = new NpgsqlCommand(
                $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                $"WHERE datname = '{databaseName}' AND pid <> pg_backend_pid()", connection))
                await terminate.ExecuteNonQueryAsync();

            await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", connection))
                await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort: a leaked uniquely named scratch database is harmless.
        }
    }
}
