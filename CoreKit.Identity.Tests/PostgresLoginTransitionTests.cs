using CoreKit.Identity.Repositories;
using Npgsql;
using Xunit.Abstractions;

namespace CoreKit.Identity.Tests;

/// <summary>
/// PostgreSQL-backed concurrency coverage for the atomic login state transitions.
/// Each test creates an isolated scratch database on the development PostgreSQL server named by
/// ConnectionStrings__Identity, runs the real EF Core/Npgsql stack against it, and drops the
/// scratch database afterwards; the configured database itself is never touched. When that
/// environment variable is missing, or a scratch database cannot be created, the test reports
/// the exact reason through <see cref="ITestOutputHelper"/> and returns without asserting —
/// SQLite results are never presented as PostgreSQL evidence.
/// </summary>
public sealed class PostgresLoginTransitionTests
{
    private const string TestEmail = "staff@example.com";
    private const string TestPassword = "StaffPass123!";
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly ITestOutputHelper _output;

    public PostgresLoginTransitionTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ConcurrentFailedLogins_OnPostgres_DoNotLoseIncrementsAndEstablishLockout()
    {
        var scratch = await PostgresScratchDb.TryCreateAsync(_output);
        if (scratch is null) return;

        await using (scratch)
        {
            await using (var setup = CreateContext(scratch.ConnectionString))
                await setup.Database.EnsureCreatedAsync();
            var userId = await SeedActiveUserAsync(scratch.ConnectionString);

            // Ten concurrent failed logins, each with its own connection, race on one row.
            var attemptCount = 10;
            var services = Enumerable.Range(0, attemptCount)
                .Select(_ => CreateAuthService(scratch.ConnectionString))
                .ToList();

            var tasks = services.Select(s => s.LoginAsync(
                new LoginRequest { Email = TestEmail, Password = "wrong-password" },
                CancellationToken.None)).ToList();
            await Task.WhenAll(tasks);

            foreach (var task in tasks)
                ResultAssert.Fails(await task, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

            await using var verify = CreateContext(scratch.ConnectionString);
            var user = await verify.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
            Assert.Equal(attemptCount, user.AccessFailedCount);
            Assert.NotNull(user.LockoutEnd);
            Assert.Equal(FixedNow.UtcDateTime.AddMinutes(15).Ticks, user.LockoutEnd.Value.Ticks);
        }
    }

    [Fact]
    public async Task Login_CorrectPassword_WhenConcurrentFailureLocksFirst_DoesNotIssueSession()
    {
        // Deterministic interleaving of the dangerous race on the production provider: the
        // successful request parks at the gate after password verification; concurrent failed
        // requests lock the account through real PostgreSQL row locking; the gate releases and
        // the authoritative transition must refuse, so no session is issued.
        var scratch = await PostgresScratchDb.TryCreateAsync(_output);
        if (scratch is null) return;

        await using (scratch)
        {
            await using (var setup = CreateContext(scratch.ConnectionString))
                await setup.Database.EnsureCreatedAsync();
            var userId = await SeedActiveUserAsync(scratch.ConnectionString);

            var atTransition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseTransition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var gatedContext = CreateContext(scratch.ConnectionString);
            var gatedRepo = new GatedUserRepository(
                new UserRepository(gatedContext),
                async () =>
                {
                    atTransition.SetResult();
                    await releaseTransition.Task;
                });
            var authService = CreateAuthService(scratch.ConnectionString, gatedRepo);

            var loginTask = authService.LoginAsync(new LoginRequest
            {
                Email = TestEmail,
                Password = TestPassword
            }, CancellationToken.None);

            // The correct-password request is parked immediately before its transition.
            await atTransition.Task;

            // Concurrent failed requests lock the account first.
            for (var i = 0; i < 5; i++)
            {
                ResultAssert.Fails(await CreateAuthService(scratch.ConnectionString).LoginAsync(new LoginRequest
                {
                    Email = TestEmail,
                    Password = "wrong-password"
                }, CancellationToken.None), IdentityErrorKind.Unauthorized, "auth.invalid_credentials");
            }

            releaseTransition.SetResult();

            // The correct-password request loses the authoritative transition and is rejected.
            ResultAssert.Fails(await loginTask, IdentityErrorKind.Unauthorized, "auth.invalid_credentials");

            await using var verify = CreateContext(scratch.ConnectionString);
            var user = await verify.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
            // The rejected attempt is counted like any attempt during an active lockout.
            Assert.Equal(6, user.AccessFailedCount);
            Assert.NotNull(user.LockoutEnd);
        }
    }

    [Fact]
    public async Task FailedLoginTransition_StaleExpiredLockoutRequest_CannotClearNewerLockout()
    {
        var scratch = await PostgresScratchDb.TryCreateAsync(_output);
        if (scratch is null) return;

        await using (scratch)
        {
            await using (var setup = CreateContext(scratch.ConnectionString))
                await setup.Database.EnsureCreatedAsync();
            await SeedActiveUserAsync(scratch.ConnectionString);

            var staleNow = FixedNow.UtcDateTime;
            var newerNow = staleNow.AddMinutes(1);

            await using (var db = CreateContext(scratch.ConnectionString))
            {
                var repo = new UserRepository(db);
                var userId = await db.Users
                    .Where(u => u.NormalizedEmail == IdentityNormalizer.NormalizeEmail(TestEmail))
                    .Select(u => u.Id)
                    .SingleAsync();

                // Seed: a lockout that is already expired as of the stale request's clock.
                await db.Users
                    .Where(u => u.Id == userId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.AccessFailedCount, 5)
                        .SetProperty(u => u.LockoutEnd, (DateTime?)staleNow.AddMinutes(-1)));

                // Concurrent failed attempts run at the newer clock and establish a fresh lockout.
                for (var i = 0; i < 5; i++)
                    Assert.True(await repo.RegisterFailedLoginAttemptAsync(
                        userId, newerNow, newerNow.AddMinutes(15), 5));

                // The stale request's transition executes afterwards with its old clock.
                Assert.True(await repo.RegisterFailedLoginAttemptAsync(
                    userId, staleNow, staleNow.AddMinutes(15), 5));
            }

            await using var verify = CreateContext(scratch.ConnectionString);
            var user = await verify.Users.AsNoTracking().SingleAsync(u => u.NormalizedEmail == IdentityNormalizer.NormalizeEmail(TestEmail));
            Assert.Equal(newerNow.AddMinutes(15).Ticks, user.LockoutEnd!.Value.Ticks);
            Assert.Equal(6, user.AccessFailedCount);
        }
    }

    [Fact]
    public async Task SuccessfulLoginTransition_StaleRequest_CannotClearNewerActiveLockout()
    {
        var scratch = await PostgresScratchDb.TryCreateAsync(_output);
        if (scratch is null) return;

        await using (scratch)
        {
            await using (var setup = CreateContext(scratch.ConnectionString))
                await setup.Database.EnsureCreatedAsync();
            await SeedActiveUserAsync(scratch.ConnectionString);

            var staleNow = FixedNow.UtcDateTime;
            var newerNow = staleNow.AddMinutes(1);

            await using (var db = CreateContext(scratch.ConnectionString))
            {
                var repo = new UserRepository(db);
                var userId = await db.Users
                    .Where(u => u.NormalizedEmail == IdentityNormalizer.NormalizeEmail(TestEmail))
                    .Select(u => u.Id)
                    .SingleAsync();

                for (var i = 0; i < 5; i++)
                    Assert.True(await repo.RegisterFailedLoginAttemptAsync(
                        userId, newerNow, newerNow.AddMinutes(15), 5));

                // A successful-login transition holding a stale clock must be refused: the newer
                // lockout is still active at the stale clock, so the authoritative transition
                // classifies the refusal as Locked and the newer lockout survives untouched.
                Assert.Equal(LoginCompletion.Locked,
                    await repo.TryCompleteSuccessfulLoginAsync(userId, 0, staleNow));
            }

            await using var verify = CreateContext(scratch.ConnectionString);
            var user = await verify.Users.AsNoTracking().SingleAsync(u => u.NormalizedEmail == IdentityNormalizer.NormalizeEmail(TestEmail));
            Assert.Equal(newerNow.AddMinutes(15).Ticks, user.LockoutEnd!.Value.Ticks);
            Assert.Equal(5, user.AccessFailedCount);
        }
    }

    private static async Task<Guid> SeedActiveUserAsync(string connectionString)
    {
        await using var db = CreateContext(connectionString);
        var hasher = new PasswordHasher();
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            Email = TestEmail,
            NormalizedEmail = IdentityNormalizer.NormalizeEmail(TestEmail),
            PasswordHash = hasher.Hash(TestPassword),
            DisplayName = "Postgres Staff",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = FixedNow.UtcDateTime
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static IAuthService CreateAuthService(string connectionString)
        => CreateAuthService(connectionString, new UserRepository(CreateContext(connectionString)));

    private static IAuthService CreateAuthService(string connectionString, IUserRepository userRepository)
        => new AuthService(
            userRepository,
            new RefreshTokenRepository(CreateContext(connectionString)),
            new PasswordHasher(),
            new FakeTokenService(),
            new FakeSessionIssuer(),
            new AccessResolver(new PermissionRepository(CreateContext(connectionString))),
            new FakeCurrentUserService(),
            Options.Create(new IdentityEmailVerificationOptions { RequireConfirmedEmail = false }),
            new FixedTimeProvider(FixedNow),
            NullLogger<AuthService>.Instance);

    private static IdentityDbContext CreateContext(string connectionString)
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options);

    /// <summary>
    /// A uniquely named scratch database created on the configured PostgreSQL server for one test
    /// and dropped again on disposal. It never touches the configured database itself, and its
    /// name is generated, so it cannot collide with real data.
    /// </summary>
    private sealed class PostgresScratchDb : IAsyncDisposable
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
            var baseConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Identity");
            if (string.IsNullOrWhiteSpace(baseConnection))
            {
                output.WriteLine("Skipped: ConnectionStrings__Identity is not set; no PostgreSQL integration coverage in this environment.");
                return null;
            }

            var databaseName = "corekit_identity_it_" + Guid.NewGuid().ToString("N")[..16];

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

                // Terminate leftover backends first; the name is generated and safe to inline.
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
}