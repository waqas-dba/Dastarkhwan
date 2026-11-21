using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.Access.Tests;

public sealed class AccessAuditActorTests : IDisposable
{
    private readonly TestEnv _env = new();

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
    public void HistoricalMigrationBackfill_ClassifiesPreExistingRowsAgainstActualMigrationSql()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Pre-migration schema: no ActorType / ActorSource columns, no check constraint.
        using (var create = connection.CreateCommand())
        {
            create.CommandText = @"
                CREATE TABLE ""ACCESS_AuditEntries"" (
                    ""Id"" TEXT NOT NULL PRIMARY KEY,
                    ""ScopeType"" TEXT NOT NULL,
                    ""ScopeId"" TEXT NOT NULL,
                    ""Action"" TEXT NOT NULL,
                    ""ActorUserId"" TEXT,
                    ""Details"" TEXT,
                    ""OccurredAt"" TEXT NOT NULL
                );";
            create.ExecuteNonQuery();
        }

        var actorUserId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        var occurred = "2026-01-01 12:00:00";
        var idWithActor = Guid.NewGuid();
        var idWithoutActor = Guid.NewGuid();

        // Add the actor columns exactly as the migration does (nullable first), then insert
        // pre-existing rows with ActorType null, simulating rows that predate the migration.
        using (var alter = connection.CreateCommand())
        {
            alter.CommandText = @"
                ALTER TABLE ""ACCESS_AuditEntries"" ADD COLUMN ""ActorType"" TEXT;
                ALTER TABLE ""ACCESS_AuditEntries"" ADD COLUMN ""ActorSource"" TEXT;
                INSERT INTO ""ACCESS_AuditEntries"" (""Id"", ""ScopeType"", ""ScopeId"", ""Action"", ""ActorUserId"", ""ActorType"", ""ActorSource"", ""Details"", ""OccurredAt"")
                VALUES (@id1, 'tenant', @scopeId, 'access.role_created', @actorUserId, NULL, NULL, 'historic', @occurred);
                INSERT INTO ""ACCESS_AuditEntries"" (""Id"", ""ScopeType"", ""ScopeId"", ""Action"", ""ActorUserId"", ""ActorType"", ""ActorSource"", ""Details"", ""OccurredAt"")
                VALUES (@id2, 'tenant', @scopeId, 'access.role_created', NULL, NULL, NULL, 'historic', @occurred);
            ";
            alter.Parameters.AddWithValue("@id1", idWithActor.ToString());
            alter.Parameters.AddWithValue("@id2", idWithoutActor.ToString());
            alter.Parameters.AddWithValue("@scopeId", scopeId.ToString());
            alter.Parameters.AddWithValue("@actorUserId", actorUserId.ToString());
            alter.Parameters.AddWithValue("@occurred", occurred);
            alter.ExecuteNonQuery();
        }

        // Apply the migration's historical backfill: non-null ActorUserId -> User, null -> Unknown.
        using (var backfill = connection.CreateCommand())
        {
            backfill.CommandText = @"
                UPDATE ""ACCESS_AuditEntries"" SET ""ActorType"" = 'User' WHERE ""ActorUserId"" IS NOT NULL;
                UPDATE ""ACCESS_AuditEntries"" SET ""ActorType"" = 'Unknown' WHERE ""ActorUserId"" IS NULL;";
            backfill.ExecuteNonQuery();
        }

        // Every row must now have a non-null ActorType (the migration makes it NOT NULL).
        using (var nullCheck = connection.CreateCommand())
        {
            nullCheck.CommandText = "SELECT COUNT(*) FROM ""ACCESS_AuditEntries"" WHERE ""ActorType"" IS NULL;";
            var nullCount = Convert.ToInt64(nullCheck.ExecuteScalar());
            Assert.Equal(0L, nullCount);
        }

        // Row with an original actor user id is classified User and every original field is preserved.
        using (var row = connection.CreateCommand())
        {
            row.CommandText = @"
                SELECT ""ActorType"", ""ActorUserId"", ""ActorSource"", ""Action"", ""Details"", ""ScopeType"", ""ScopeId"", ""OccurredAt""
                FROM ""ACCESS_AuditEntries"" WHERE ""Id"" = @id1;";
            row.Parameters.AddWithValue("@id1", idWithActor.ToString());
            using var reader = row.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(AccessAuditEntry.ActorTypeUser, reader.GetString(0));
            Assert.Equal(actorUserId.ToString(), reader.GetString(1));
            Assert.True(reader.IsDBNull(2)); // ActorSource remains null
            Assert.Equal("access.role_created", reader.GetString(3));
            Assert.Equal("historic", reader.GetString(4));
            Assert.Equal("tenant", reader.GetString(5));
            Assert.Equal(scopeId.ToString(), reader.GetString(6));
            Assert.Equal(occurred, reader.GetString(7));
        }

        // Row without an actor user id is classified Unknown; no system actor or origin is invented.
        using (var row = connection.CreateCommand())
        {
            row.CommandText = @"
                SELECT ""ActorType"", ""ActorUserId"", ""ActorSource""
                FROM ""ACCESS_AuditEntries"" WHERE ""Id"" = @id2;";
            row.Parameters.AddWithValue("@id2", idWithoutActor.ToString());
            using var reader = row.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(AccessAuditEntry.ActorTypeUnknown, reader.GetString(0));
            Assert.DBNull.Value.Equals(reader.GetValue(1));
            Assert.DBNull.Value.Equals(reader.GetValue(2));
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
    public void ActorCheckConstraint_IsConfiguredInModelMetadata()
    {
        using var context = _env.NewContext();
        var entity = Assert.NotNull(context.Model.FindEntityType(typeof(AccessAuditEntry)));
        Assert.Contains(entity.GetCheckConstraints(), c => c.Name == "CK_ACCESS_AuditEntries_Actor");
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
