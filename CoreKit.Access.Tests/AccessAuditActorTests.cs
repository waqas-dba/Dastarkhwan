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

        // Simulate a row after the migration backfill: classification is derived from ActorUserId,
        // and every original field is preserved.
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

        await _env.CreateMaintenance().BootstrapInitialPlatformAdminAsync(targetUser);

        var entry = _env.Read(db => db.AuditEntries.Single(a => a.Action == "platform.admin.bootstrap"));
        Assert.Equal(AccessAuditEntry.ActorTypeSystem, entry.ActorType);
        Assert.Null(entry.ActorUserId);
        Assert.Equal("deployment.bootstrap", entry.ActorSource);
        Assert.Contains(targetUser.ToString(), entry.Details);
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
