using CoreKit.Tenant.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CoreKit.Tenant.Tests;

/// <summary>
/// What this protects: the committed Tenant migration snapshot stays faithful to the live TenantDbContext model
/// for the metadata a migration cannot show - the TenantMember soft-delete query filter - and the transactional
/// outbox is actually mapped (table, columns, primary key, indexes) with no foreign key to the tenant.
/// Why it matters: the outbox model shipped before its migration. If the snapshot and the live model disagree,
/// the next generated migration would contain changes nobody asked for (or silently miss the outbox table), and
/// a foreign key to TENANT_Tenants would delete a tenant's undelivered events with the tenant row. A query
/// filter is runtime EF query behavior, not a schema object, so dropping it from the snapshot never shows up as
/// a pending model change yet changes the model every later migration is generated from.
/// The rule under test (see TenantDbContext.OnModelCreating, TenantOutboxMessage and the
/// 20261116100911_AddTenantOutboxSchema migration): TENANT_OutboxMessages carries every TenantOutboxMessage
/// property, its primary key is EventId, the per-tenant sequence index is unique, there is no tenant FK, and
/// TenantMember keeps its RemovedAt == null filter in both the live model and the committed snapshot.
/// </summary>
public sealed class MigrationSnapshotTests
{
    private const string OutboxEntityName = "CoreKit.Tenant.Entities.TenantOutboxMessage";
    private const string TenantEntityName = "CoreKit.Tenant.Entities.TenantEntity";
    private const string MemberEntityName = "CoreKit.Tenant.Entities.TenantMember";

    private const string OutboxTable = "TENANT_OutboxMessages";
    private const string TenantTable = "TENANT_Tenants";

    private static readonly (string Name, Type ClrType, bool Nullable, int? MaxLength)[] OutboxColumns =
    {
        ("EventId", typeof(Guid), false, null),
        ("TenantId", typeof(Guid), false, null),
        ("Sequence", typeof(long), false, null),
        ("EventType", typeof(string), false, 100),
        ("SchemaVersion", typeof(int), false, null),
        ("SubjectUserId", typeof(Guid?), true, null),
        ("MembershipGeneration", typeof(long?), true, null),
        ("PayloadJson", typeof(string), false, 4000),
        ("OccurredAt", typeof(DateTime), false, null),
        ("CreatedAt", typeof(DateTime), false, null),
        ("Status", typeof(TenantOutboxStatus), false, 20),
        ("AttemptCount", typeof(int), false, null),
        ("NextAttemptAt", typeof(DateTime), false, null),
        ("LeaseOwner", typeof(string), true, 100),
        ("LeaseExpiresAt", typeof(DateTime?), true, null),
        ("LastAttemptAt", typeof(DateTime?), true, null),
        ("DeliveredAt", typeof(DateTime?), true, null),
        ("DeadLetteredAt", typeof(DateTime?), true, null),
        ("LastError", typeof(string), true, 2000)
    };

    /// <summary>
    /// The committed snapshot is generated from the live model. This compares table and column names only; the
    /// query filter and the outbox constraints are asserted separately below.
    /// </summary>
    [Fact]
    public void TheCommittedSnapshotMatchesTheLiveModel()
    {
        Assert.Equal(TableShape(BuildSnapshotModel()), TableShape(BuildLiveModel()));
    }

    /// <summary>
    /// A query filter is runtime query behavior, so its loss never appears as a pending model change. Both the
    /// committed snapshot and the live model must keep excluding soft-deleted members.
    /// </summary>
    [Fact]
    public void TenantMember_KeepsTheSoftDeleteQueryFilter()
    {
        AssertSoftDeleteFilter(BuildSnapshotModel());
        AssertSoftDeleteFilter(BuildLiveModel());
    }

    [Fact]
    public void TenantEntity_HasRequired_LastEventSequence()
    {
        AssertLastEventSequence(BuildSnapshotModel());
        AssertLastEventSequence(BuildLiveModel());
    }

    [Fact]
    public void TenantOutboxMessage_IsMappedToTheOutboxTable_WithEveryColumn()
    {
        AssertOutboxTable(BuildSnapshotModel(), snapshot: true);
        AssertOutboxTable(BuildLiveModel(), snapshot: false);
    }

    [Fact]
    public void TenantOutboxMessage_HasPrimaryKeyOnEventId()
    {
        AssertPrimaryKey(BuildSnapshotModel());
        AssertPrimaryKey(BuildLiveModel());
    }

    [Fact]
    public void TenantOutboxMessage_HasTheSequenceAndClaimScanIndexes()
    {
        AssertIndexes(BuildSnapshotModel());
        AssertIndexes(BuildLiveModel());
    }

    [Fact]
    public void TenantOutboxMessage_HasNoForeignKeyToTheTenantTable()
    {
        AssertNoTenantForeignKey(BuildSnapshotModel());
        AssertNoTenantForeignKey(BuildLiveModel());
    }

    /// <summary>
    /// Accepts both EF-generated shapes of "not removed": the live model's <c>m =&gt; m.RemovedAt == null</c>
    /// and the snapshot's <c>!EF.Property&lt;DateTime?&gt;(__filteredEntity, "RemovedAt").HasValue</c>. Either
    /// shape must mention RemovedAt and test it against null; a filter meaning "removed" fails.
    /// </summary>
    private static void AssertSoftDeleteFilter(IModel model)
    {
        var member = model.FindEntityType(MemberEntityName);
        Assert.NotNull(member);

        var removedAt = member!.FindProperty("RemovedAt");
        Assert.NotNull(removedAt);
        Assert.True(removedAt!.IsNullable, "RemovedAt must stay nullable for the soft-delete filter to work.");

        var filters = member.GetDeclaredQueryFilters();
        Assert.NotEmpty(filters);

        var expression = filters.Single().Expression!.ToString();
        Assert.Contains("RemovedAt", expression, StringComparison.Ordinal);

        var testsNull = expression.Contains("== null", StringComparison.Ordinal)
            || (expression.Contains("\"RemovedAt\"", StringComparison.Ordinal)
                && expression.Contains("HasValue", StringComparison.Ordinal)
                && (expression.Contains('!') || expression.Contains("Not(", StringComparison.Ordinal)));

        Assert.True(
            testsNull,
            $"The TenantMember query filter must exclude soft-deleted rows (RemovedAt == null). Actual: {expression}");
    }

    private static void AssertLastEventSequence(IModel model)
    {
        var tenant = model.FindEntityType(TenantEntityName);
        Assert.NotNull(tenant);

        var sequence = tenant!.FindProperty("LastEventSequence");
        Assert.NotNull(sequence);
        Assert.Equal(typeof(long), sequence!.ClrType);
        Assert.False(sequence.IsNullable);
        Assert.Equal(TenantTable, tenant.GetTableName());
    }

    private static void AssertOutboxTable(IModel model, bool snapshot)
    {
        var outbox = model.FindEntityType(OutboxEntityName);
        Assert.NotNull(outbox);
        Assert.Equal(OutboxTable, outbox!.GetTableName());

        var actual = outbox.GetProperties()
            .ToDictionary(
                p => p.Name,
                p => (ClrType: p.ClrType, Nullable: p.IsNullable, MaxLength: p.GetMaxLength()),
                StringComparer.Ordinal);

        Assert.Equal(OutboxColumns.Length, actual.Count);

        foreach (var (name, clrType, nullable, maxLength) in OutboxColumns)
        {
            Assert.True(actual.ContainsKey(name), $"The outbox table is missing the '{name}' column.");

            // The snapshot stores a value-converted property under its provider type, so the enum column
            // reads back as string there and as the enum in the live model.
            var expectedType = snapshot && clrType.IsEnum ? typeof(string) : clrType;

            Assert.Equal(expectedType, actual[name].ClrType);
            Assert.Equal(nullable, actual[name].Nullable);
            Assert.Equal(maxLength, actual[name].MaxLength);
        }
    }

    private static void AssertPrimaryKey(IModel model)
    {
        var outbox = model.FindEntityType(OutboxEntityName);
        Assert.NotNull(outbox);

        var primaryKey = outbox!.FindPrimaryKey();
        Assert.NotNull(primaryKey);
        Assert.Equal(new[] { "EventId" }, primaryKey!.Properties.Select(p => p.Name).ToArray());
    }

    private static void AssertIndexes(IModel model)
    {
        var outbox = model.FindEntityType(OutboxEntityName);
        Assert.NotNull(outbox);

        var sequence = outbox!.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "UX_TENANT_OutboxMessages_Tenant_Sequence");
        Assert.NotNull(sequence);
        Assert.True(sequence!.IsUnique);
        Assert.Equal(new[] { "TenantId", "Sequence" }, sequence.Properties.Select(p => p.Name).ToArray());

        var claimScan = outbox.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "IX_TENANT_OutboxMessages_Status_NextAttempt");
        Assert.NotNull(claimScan);
        Assert.False(claimScan!.IsUnique);
        Assert.Equal(new[] { "Status", "NextAttemptAt" }, claimScan.Properties.Select(p => p.Name).ToArray());
    }

    private static void AssertNoTenantForeignKey(IModel model)
    {
        var outbox = model.FindEntityType(OutboxEntityName);
        Assert.NotNull(outbox);

        // A tenant-deletion event must outlive its tenant row, so no FK may cascade it away.
        Assert.Empty(outbox!.GetForeignKeys());
    }

    /// <summary>Every mapped table with its columns, independent of the database provider's store types.</summary>
    private static Dictionary<string, string[]> TableShape(IModel model)
        => model.GetEntityTypes()
            .Where(t => t.GetTableName() is not null)
            .ToDictionary(
                t => t.GetTableName()!,
                t => t.GetProperties()
                    .Select(p => p.GetColumnName()!)
                    .OrderBy(c => c, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

    private static IModel BuildSnapshotModel()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        using var db = new TenantDbContext(options);
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        return migrationsAssembly.ModelSnapshot!.Model;
    }

    private static IModel BuildLiveModel()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        using var db = new TenantDbContext(options);
        return db.Model;
    }
}