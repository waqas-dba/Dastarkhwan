using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit.Abstractions;

namespace CoreKit.Access.Tests;

public sealed class AccessAuditActorTests : IDisposable
{
    private readonly TestEnv _env;
    private readonly ITestOutputHelper _output;

    public AccessAuditActorTests(ITestOutputHelper output)
    {
        _output = output;
        _env = new TestEnv();
    }

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task User_Classification_Includes_ActorUserId_And_No_Source()
    {
        var userId = _env.SignInOwner();

        _env.Audit.Record(_env.Scope, AccessAuditActions.UserRemoved, $"user: {userId}");
        await _env.UnitOfWork.SaveAsync();

        var entry = _env.Read(db => db.AuditEntries.Single());
        Assert.Equal(AccessAuditEntry.ActorTypeUser, entry.ActorType);
        Assert.Equal(userId, entry.ActorUserId);
        Assert.Null(entry.ActorSource);
    }

    [Fact]
    public async Task System_Actor_Records_Source_With_Null_ActorUserId()
    {
        var source = "deployment.bootstrap";

        _env.Audit.RecordSystem(_env.Scope, "platform.admin.bootstrap", source, "user: 00000000-0000-0000-0000-000000000000");
        await _env.UnitOfWork.SaveAsync();

        var entry = _env.Read(db => db.AuditEntries.Single());
        Assert.Equal(AccessAuditEntry.ActorTypeSystem, entry.ActorType);
        Assert.Null(entry.ActorUserId);
        Assert.Equal(source, entry.ActorSource);
        Assert.Equal("platform.admin.bootstrap", entry.Action);
    }

    [Fact]
    public async Task Unknown_Actor_Does_Not_Invent_Identity()
    {
        // No one is signed in.
        _env.Audit.Record(_env.Scope, AccessAuditActions.ScopeProvisioned, "roles: 2");
        await _env.UnitOfWork.SaveAsync();

        var entry = _env.Read(db => db.AuditEntries.Single());
        Assert.Equal(AccessAuditEntry.ActorTypeUnknown, entry.ActorType);
        Assert.Null(entry.ActorUserId);
        Assert.Null(entry.ActorSource);
    }

    [Fact]
    public void Historical_Classification_Preserves_Every_Existing_Field()
    {
        var userId = Guid.NewGuid();
        var occurred = _env.UtcNow;

        _env.Seed(db => db.AuditEntries.Add(new AccessAuditEntry
        {
            Id = Guid.NewGuid(),
            ScopeType = _env.Scope.Type,
            ScopeId = _env.Scope.Id,
            Action = "earlier",
            ActorUserId = userId,
            ActorType = AccessAuditEntry.ClassifyHistoricalActor(userId),
            Details = "historic",
            OccurredAt = occurred
        }));

        var row = _env.Read(db => db.AuditEntries.Single());

        // Historical rule applied by the migration: non-null actor user id -> User, null -> Unknown.
        Assert.Equal(AccessAuditEntry.ClassifyHistoricalActor(userId), AccessAuditEntry.ActorTypeUser);
        Assert.Equal(AccessAuditEntry.ClassifyHistoricalActor(null), AccessAuditEntry.ActorTypeUnknown);

        // Original fields are preserved; no system actor, operator identity, source, or origin is invented.
        Assert.Equal(userId, row.ActorUserId);
        Assert.Equal(AccessAuditEntry.ActorTypeUser, row.ActorType);
        Assert.Null(row.ActorSource);
        Assert.Equal("earlier", row.Action);
        Assert.Equal("historic", row.Details);
        Assert.Equal(_env.Scope.Type, row.ScopeType);
        Assert.Equal(_env.Scope.Id, row.ScopeId);
        Assert.Equal(occurred, row.OccurredAt);
    }

    [Fact]
    public async Task Bootstrap_Records_System_Actor_Not_The_Target_User()
    {
        var targetUser = Guid.NewGuid();

        // Bootstrap is reached through the trusted bootstrap service, not through IAccessMaintenance.
        await _env.CreateBootstrap().BootstrapInitialPlatformAdminAsync(targetUser);

        var entry = _env.Read(db => db.AuditEntries.Single(a => a.Action == "platform.admin.bootstrap"));
        Assert.Equal(AccessAuditEntry.ActorTypeSystem, entry.ActorType);
        Assert.Null(entry.ActorUserId);
        Assert.Equal("deployment.bootstrap", entry.ActorSource);
        Assert.Contains(targetUser.ToString(), entry.Details);
    }

    [Fact]
    public void HistoricalActorClassification_IsDerivedFromActorUserId()
    {
        // Classification helper used by the migration backfill: non-null actor user id -> User,
        // null -> Unknown. No system actor, operator identity, source, or origin is invented.
        var userId = Guid.NewGuid();
        Assert.Equal(AccessAuditEntry.ActorTypeUser, AccessAuditEntry.ClassifyHistoricalActor(userId));
        Assert.Equal(AccessAuditEntry.ActorTypeUnknown, AccessAuditEntry.ClassifyHistoricalActor(null));
    }

    [Fact]
    public async Task HistoricalMigrationBackfill_AppliesActualEFMigration_OnPostgres()
    {
        var scratch = await PostgresScratchDb.TryCreateAsync(_output);
        if (scratch is null)
        {
            // Optional mode: PostgreSQL is not available. No assertions are executed so this
            // is never mistaken for a passing test. In required mode
            // (COREKIT_REQUIRE_POSTGRES=1) TryCreateAsync throws above, failing the test.
            _output.WriteLine("Skipped: PostgreSQL migration test infrastructure unavailable; no assertions executed.");
            return;
        }

        await using (scratch)
        {
            await using var connection = new NpgsqlConnection(scratch.ConnectionString);
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AccessDbContext>()
                .UseNpgsql(scratch.ConnectionString,
                    npgsql => npgsql.MigrationsHistoryTable(AccessDbContextConfigurator.MigrationsHistoryTable))
                .Options;

            await using var context = new AccessDbContext(options);
            var migrator = context.Database.GetService<IMigrator>();

            // Migrate to the state just before the actor-schema migration: no ActorType / ActorUserId / ActorSource columns.
            migrator.Migrate("20261116043029_AddAccessMembershipStreamAndFreshness");

            // Insert pre-existing historical rows. ActorUserId column does not exist yet, so it is omitted.
            var scopeId = Guid.NewGuid();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            await using (var insert = new NpgsqlCommand(@"
                INSERT INTO ""ACCESS_AuditEntries"" (""Id"", ""ScopeType"", ""ScopeId"", ""Action"", ""Details"", ""OccurredAt"")
                VALUES (@id1, 'tenant', @scopeId, 'access.role_created', 'historic', '2026-01-01 12:00:00+00'),
                        (@id2, 'tenant', @scopeId, 'access.role_created', 'historic', '2026-01-01 12:00:00+00');", connection))
            {
                insert.Parameters.AddWithValue("@id1", id1);
                insert.Parameters.AddWithValue("@id2", id2);
                insert.Parameters.AddWithValue("@scopeId", scopeId);
                await insert.ExecuteNonQueryAsync();
            }

            // Apply the ACTUAL actor-schema migration (adds columns, backfills ActorType from ActorUserId).
            migrator.Migrate("20261121130000_AddAccessAuditActorSchema");

            // Every row must now have a non-null ActorType (backfilled by the migration).
            await using (var nullCheck = new NpgsqlCommand(
                "SELECT COUNT(*) FROM \"ACCESS_AuditEntries\" WHERE \"ActorType\" IS NULL;", connection))
            {
                var nullCount = (long)await nullCheck.ExecuteScalarAsync();
                Assert.Equal(0L, nullCount);
            }

            // All pre-existing rows had no ActorUserId, so all are backfilled to Unknown;
            // original fields are preserved.
            await using (var row1 = new NpgsqlCommand(@"
                SELECT ""ActorType"", ""ActorUserId"", ""ActorSource"", ""Action"", ""Details"", ""ScopeType"", ""ScopeId"", ""OccurredAt""
                FROM ""ACCESS_AuditEntries"" WHERE ""Id"" = @id;", connection))
            {
                row1.Parameters.AddWithValue("@id", id1);
                await using var reader = await row1.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(AccessAuditEntry.ActorTypeUnknown, reader.GetString(0));
                Assert.True(reader.IsDBNull(1)); // ActorUserId is NULL
                Assert.True(reader.IsDBNull(2)); // ActorSource is NULL
                Assert.Equal("access.role_created", reader.GetString(3));
                Assert.Equal("historic", reader.GetString(4));
                Assert.Equal("tenant", reader.GetString(5));
                Assert.Equal(scopeId, reader.GetGuid(6));
            }

            await using (var row2 = new NpgsqlCommand(@"
                SELECT ""ActorType"", ""ActorUserId"", ""ActorSource""
                FROM ""ACCESS_AuditEntries"" WHERE ""Id"" = @id;", connection))
            {
                row2.Parameters.AddWithValue("@id", id2);
                await using var reader = await row2.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(AccessAuditEntry.ActorTypeUnknown, reader.GetString(0));
                Assert.True(reader.IsDBNull(1));
                Assert.True(reader.IsDBNull(2));
            }

            // Apply the check-constraint migration, then the whitespace corrective migration.
            migrator.Migrate("20261121160000_AddAccessAuditActorCheckConstraint");
            migrator.Migrate("20261121170000_CorrectAccessAuditActorWhitespaceConstraint");

            // Verify the database constraint rejects invalid combinations. Each assertion uses its own
            // context instance so a failed SaveChanges leaves no tracked entities that could contaminate
            // subsequent assertions.
            AssertConstraintRejected(options, scopeId, new AccessAuditEntry
            {
                ActorType = AccessAuditEntry.ActorTypeUser,
                Action = "invalid",
                OccurredAt = DateTimeOffset.UtcNow.UtcDateTime
            });

            // Spaces-only source must be rejected by trim-based constraint.
            AssertConstraintRejected(options, scopeId, new AccessAuditEntry
            {
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorSource = "   ",
                Action = "invalid",
                OccurredAt = DateTimeOffset.UtcNow.UtcDateTime
            });

            // Tab-only source must be rejected.
            AssertConstraintRejected(options, scopeId, new AccessAuditEntry
            {
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorSource = "\t",
                Action = "invalid",
                OccurredAt = DateTimeOffset.UtcNow.UtcDateTime
            });

            // Carriage-return-only source must be rejected.
            AssertConstraintRejected(options, scopeId, new AccessAuditEntry
            {
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorSource = "\r",
                Action = "invalid",
                OccurredAt = DateTimeOffset.UtcNow.UtcDateTime
            });

            // Line-feed-only source must be rejected.
            AssertConstraintRejected(options, scopeId, new AccessAuditEntry
            {
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorSource = "\n",
                Action = "invalid",
                OccurredAt = DateTimeOffset.UtcNow.UtcDateTime
            });

            // Valid nonblank source with surrounding whitespace must be accepted.
            var acceptedId = Guid.NewGuid();
            using (var acceptedContext = new AccessDbContext(options))
            {
                acceptedContext.AuditEntries.Add(new AccessAuditEntry
                {
                    Id = acceptedId,
                    ScopeType = "tenant",
                    ScopeId = scopeId,
                    Action = "platform.admin.bootstrap",
                    ActorType = AccessAuditEntry.ActorTypeSystem,
                    ActorSource = "  deployment.bootstrap  ",
                    OccurredAt = DateTimeOffset.UtcNow.UtcDateTime
                });
                acceptedContext.SaveChanges();
            }
        }
    }

    private static void AssertConstraintRejected(
        DbContextOptions<AccessDbContext> options, Guid scopeId, AccessAuditEntry entry)
    {
        // Each call uses a fresh context so a failed SaveChanges leaves no tracked entities
        // that could contaminate subsequent assertions.
        entry.Id = Guid.NewGuid();
        entry.ScopeType = "tenant";
        entry.ScopeId = scopeId;
        using var context = new AccessDbContext(options);
        context.AuditEntries.Add(entry);
        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void InvalidActorCombination_RejectedByDatabaseConstraint()
    {
        // Bypasses the recorder's application-level validation and writes entity metadata directly,
        // so the database check constraint is the enforcement backstop.
        Assert.Throws<DbUpdateException>(() => _env.Seed(db =>
        {
            db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "invalid",
                ActorType = AccessAuditEntry.ActorTypeUser,
                // User actor with a null ActorUserId and null ActorSource is not a permitted combination.
                OccurredAt = _env.UtcNow
            });
        }));
    }

    [Fact]
    public void System_Actor_With_ActorUserId_RejectedByDatabaseConstraint()
    {
        Assert.Throws<DbUpdateException>(() => _env.Seed(db =>
        {
            db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "invalid",
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorUserId = Guid.NewGuid(),
                ActorSource = "deployment.bootstrap",
                OccurredAt = _env.UtcNow
            });
        }));
    }

    [Fact]
    public void Unknown_Actor_With_ActorSource_RejectedByDatabaseConstraint()
    {
        Assert.Throws<DbUpdateException>(() => _env.Seed(db =>
        {
            db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "invalid",
                ActorType = AccessAuditEntry.ActorTypeUnknown,
                ActorSource = "some-source",
                OccurredAt = _env.UtcNow
            });
        }));
    }

    [Fact]
    public void System_Actor_With_Blank_Source_RejectedByDatabaseConstraint()
    {
        Assert.Throws<DbUpdateException>(() => _env.Seed(db =>
        {
            db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "invalid",
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorSource = "   ",
                OccurredAt = _env.UtcNow
            });
        }));
    }

    [Fact]
    public void System_Actor_With_Tab_Source_RejectedByDatabaseConstraint()
    {
        Assert.Throws<DbUpdateException>(() => _env.Seed(db =>
        {
            db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "invalid",
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorSource = "\t",
                OccurredAt = _env.UtcNow
            });
        }));
    }

    [Fact]
    public void System_Actor_With_CarriageReturn_Source_RejectedByDatabaseConstraint()
    {
        Assert.Throws<DbUpdateException>(() => _env.Seed(db =>
        {
            db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "invalid",
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorSource = "\r",
                OccurredAt = _env.UtcNow
            });
        }));
    }

    [Fact]
    public void System_Actor_With_LineFeed_Source_RejectedByDatabaseConstraint()
    {
        Assert.Throws<DbUpdateException>(() => _env.Seed(db =>
        {
            db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "invalid",
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorSource = "\n",
                OccurredAt = _env.UtcNow
            });
        }));
    }

    [Fact]
    public void System_Actor_With_MixedWhitespace_Source_RejectedByDatabaseConstraint()
    {
        Assert.Throws<DbUpdateException>(() => _env.Seed(db =>
        {
            db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "invalid",
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorSource = " \t\r\n ",
                OccurredAt = _env.UtcNow
            });
        }));
    }

    [Fact]
    public void System_Actor_With_Nonblank_Source_AcceptedByDatabaseConstraint()
    {
        _env.Seed(db =>
        {
            db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "platform.admin.bootstrap",
                ActorType = AccessAuditEntry.ActorTypeSystem,
                ActorSource = "deployment.bootstrap",
                OccurredAt = _env.UtcNow
            });
        });

        var count = _env.Read(db => db.AuditEntries.Count(e => e.ActorSource == "deployment.bootstrap"));
        Assert.Equal(1, count);
    }

    [Fact]
    public void System_Actor_With_WhitespaceOnly_Source_RejectedByValidator()
    {
        Assert.Throws<ArgumentException>(() =>
            AccessAuditEntry.ValidateActor(AccessAuditEntry.ActorTypeSystem, null, " \t\r\n"));
    }

    [Fact]
    public void System_Actor_With_TabOnly_Source_RejectedByValidator()
    {
        Assert.Throws<ArgumentException>(() =>
            AccessAuditEntry.ValidateActor(AccessAuditEntry.ActorTypeSystem, null, "\t"));
    }

    [Fact]
    public void Unknown_Actor_With_Whitespace_Source_RejectedByValidator()
    {
        Assert.Throws<ArgumentException>(() =>
            AccessAuditEntry.ValidateActor(AccessAuditEntry.ActorTypeUnknown, null, " \t"));
    }

    [Fact]
    public void Unknown_Actor_Type_RejectedByDatabaseConstraint()
    {
        Assert.Throws<DbUpdateException>(() => _env.Seed(db =>
        {
            db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "invalid",
                ActorType = "Unit",
                OccurredAt = _env.UtcNow
            });
        }));
    }

    [Fact]
    public void User_Actor_Without_A_UserId_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AccessAuditEntry.ValidateActor(AccessAuditEntry.ActorTypeUser, null, null));
    }

    [Fact]
    public void User_Actor_With_Empty_UserId_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AccessAuditEntry.ValidateActor(AccessAuditEntry.ActorTypeUser, Guid.Empty, null));
    }

    [Fact]
    public void System_Actor_With_A_UserId_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AccessAuditEntry.ValidateActor(AccessAuditEntry.ActorTypeSystem, Guid.NewGuid(), null));
    }

    [Fact]
    public void Unknown_Actor_With_A_UserId_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AccessAuditEntry.ValidateActor(AccessAuditEntry.ActorTypeUnknown, Guid.NewGuid(), null));
    }

    [Fact]
    public void Unknown_Actor_Type_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AccessAuditEntry.ValidateActor("Unit", null, null));
    }

    [Fact]
    public void System_Actor_Without_A_Source_Is_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AccessAuditEntry.ValidateActor(AccessAuditEntry.ActorTypeSystem, null, null));
    }

    [Fact]
    public void User_Actor_With_A_Source_Is_Rejected()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            AccessAuditEntry.ValidateActor(AccessAuditEntry.ActorTypeUser, userId, "deployment.bootstrap"));
    }
}
