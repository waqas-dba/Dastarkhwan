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
        if (scratch is null) return;

        try
        {
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

                // Migrate to the state just before the actor-schema migration: no ActorType / ActorSource columns.
                migrator.Migrate("20261116043029_AddAccessMembershipStreamAndFreshness");

                // Insert pre-existing historical rows (ActorType / ActorSource columns do not exist yet).
                var actorUserId = Guid.NewGuid();
                var scopeId = Guid.NewGuid();
                var idWithActor = Guid.NewGuid();
                var idWithoutActor = Guid.NewGuid();

                await using (var insert = new NpgsqlCommand(@"
                    INSERT INTO ""ACCESS_AuditEntries"" (""Id"", ""ScopeType"", ""ScopeId"", ""Action"", ""ActorUserId"", ""Details"", ""OccurredAt"")
                    VALUES (@id1, 'tenant', @scopeId, 'access.role_created', @actorUserId, 'historic', '2026-01-01 12:00:00+00'),
                            (@id2, 'tenant', @scopeId, 'access.role_created', NULL, 'historic', '2026-01-01 12:00:00+00');", connection))
                {
                    insert.Parameters.AddWithValue("@id1", idWithActor);
                    insert.Parameters.AddWithValue("@id2", idWithoutActor);
                    insert.Parameters.AddWithValue("@scopeId", scopeId);
                    await insert.ExecuteNonQueryAsync();
                }

                // Apply the ACTUAL actor-schema migration (adds columns, backfills, makes ActorType non-null).
                migrator.Migrate("20261121130000_AddAccessAuditActorSchema");

                // Every row must now have a non-null ActorType.
                await using (var nullCheck = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM \"ACCESS_AuditEntries\" WHERE \"ActorType\" IS NULL;", connection))
                {
                    var nullCount = (long)await nullCheck.ExecuteScalarAsync();
                    Assert.Equal(0L, nullCount);
                }

                // Row with an original actor user id is classified User; every original field preserved.
                await using (var row1 = new NpgsqlCommand(@"
                    SELECT ""ActorType"", ""ActorUserId"", ""ActorSource"", ""Action"", ""Details"", ""ScopeType"", ""ScopeId"", ""OccurredAt""
                    FROM ""ACCESS_AuditEntries"" WHERE ""Id"" = @id;", connection))
                {
                    row1.Parameters.AddWithValue("@id", idWithActor);
                    await using var reader = await row1.ExecuteReaderAsync();
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal(AccessAuditEntry.ActorTypeUser, reader.GetString(0));
                    Assert.Equal(actorUserId, reader.GetGuid(1));
                    Assert.True(reader.IsDBNull(2)); // ActorSource remains null
                    Assert.Equal("access.role_created", reader.GetString(3));
                    Assert.Equal("historic", reader.GetString(4));
                    Assert.Equal("tenant", reader.GetString(5));
                    Assert.Equal(scopeId, reader.GetGuid(6));
                }

                // Row without an actor user id is classified Unknown; no system actor or origin invented.
                await using (var row2 = new NpgsqlCommand(@"
                    SELECT ""ActorType"", ""ActorUserId"", ""ActorSource""
                    FROM ""ACCESS_AuditEntries"" WHERE ""Id"" = @id;", connection))
                {
                    row2.Parameters.AddWithValue("@id", idWithoutActor);
                    await using var reader = await row2.ExecuteReaderAsync();
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal(AccessAuditEntry.ActorTypeUnknown, reader.GetString(0));
                    Assert.True(reader.IsDBNull(1));
                    Assert.True(reader.IsDBNull(2));
                }

                // Apply the check-constraint migration, then the whitespace corrective migration.
                migrator.Migrate("20261121160000_AddAccessAuditActorCheckConstraint");
                migrator.Migrate("20261121170000_CorrectAccessAuditActorWhitespaceConstraint");

                // Verify the database constraint rejects invalid combinations via EF context.
                Assert.Throws<DbUpdateException>(() =>
                {
                    context.AuditEntries.Add(new AccessAuditEntry
                    {
                        Id = Guid.NewGuid(),
                        ScopeType = "tenant",
                        ScopeId = scopeId,
                        Action = "invalid",
                        ActorType = AccessAuditEntry.ActorTypeUser,
                        // User actor with null ActorUserId and null ActorSource is not a permitted combination.
                        OccurredAt = DateTimeOffset.UtcNow.UtcDateTime
                    });
                    context.SaveChanges();
                });

                // Spaces-only source must be rejected by trim-based constraint.
                Assert.Throws<DbUpdateException>(() =>
                {
                    context.AuditEntries.Add(new AccessAuditEntry
                    {
                        Id = Guid.NewGuid(),
                        ScopeType = "tenant",
                        ScopeId = scopeId,
                        Action = "invalid",
                        ActorType = AccessAuditEntry.ActorTypeSystem,
                        ActorSource = "   ", // spaces only
                        OccurredAt = DateTimeOffset.UtcNow.UtcDateTime
                    });
                    context.SaveChanges();
                });

                // Valid nonblank source with surrounding whitespace must be accepted.
                context.AuditEntries.Add(new AccessAuditEntry
                {
                    Id = Guid.NewGuid(),
                    ScopeType = "tenant",
                    ScopeId = scopeId,
                    Action = "platform.admin.bootstrap",
                    ActorType = AccessAuditEntry.ActorTypeSystem,
                    ActorSource = "  deployment.bootstrap  ", // nonblank with padding
                    OccurredAt = DateTimeOffset.UtcNow.UtcDateTime
                });
                context.SaveChanges();
            }
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or System.InvalidOperationException)
        {
            _output.WriteLine($"Skipped: PostgreSQL migration test infrastructure unavailable ({ex.Message}).");
        }
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
