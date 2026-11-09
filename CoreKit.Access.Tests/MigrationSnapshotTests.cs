using CoreKit.Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CoreKit.Access.Tests;

/// <summary>
/// What this protects: the EF Core model snapshot stays in sync with the AccessDbContext model
/// and the latest migration target model. Specifically the composite alternate key on AccessRole
/// (Id, ScopeType, ScopeId) that supports the composite foreign key from AccessAssignment.
/// Why it matters: if the snapshot loses the alternate key, EF would generate a different migration
/// or fail to detect model changes, breaking the scope-consistency constraint at the database level.
/// The rule under test (see AccessDbContext.OnModelCreating and the 20261110090000 migration):
/// AccessRole has a composite alternate key (Id, ScopeType, ScopeId), AccessAssignment references
/// it through a composite foreign key (RoleId, ScopeType, ScopeId), and AccessRole has a unique
/// index on (ScopeType, ScopeId, NormalizedName).
/// </summary>
public sealed class MigrationSnapshotTests
{
    [Fact]
    public void AccessRole_HasCompositeAlternateKey_Id_ScopeType_ScopeId()
    {
        var model = BuildModel();

        var role = model.FindEntityType("CoreKit.Access.Entities.AccessRole");
        Assert.NotNull(role);

        var alternateKey = role.GetKeys()
            .FirstOrDefault(k => k.Properties.Count == 3 &&
                k.Properties.Select(p => p.Name).SequenceEqual(new[] { "Id", "ScopeType", "ScopeId" }));

        Assert.NotNull(alternateKey);
    }

    [Fact]
    public void AccessAssignment_HasCompositeForeignKey_RoleId_ScopeType_ScopeId_To_AccessRole()
    {
        var model = BuildModel();

        var assignment = model.FindEntityType("CoreKit.Access.Entities.AccessAssignment");
        Assert.NotNull(assignment);

        var role = model.FindEntityType("CoreKit.Access.Entities.AccessRole");
        Assert.NotNull(role);

        var principalKey = role.GetKeys()
            .FirstOrDefault(k => k.Properties.Count == 3 &&
                k.Properties.Select(p => p.Name).SequenceEqual(new[] { "Id", "ScopeType", "ScopeId" }));
        Assert.NotNull(principalKey);

        var foreignKey = assignment.GetForeignKeys()
            .FirstOrDefault(f => f.Properties.Count == 3 &&
                f.Properties.Select(p => p.Name).SequenceEqual(new[] { "RoleId", "ScopeType", "ScopeId" }) &&
                f.PrincipalKey.Properties.Count == 3 &&
                f.PrincipalKey.Properties.Select(p => p.Name).SequenceEqual(new[] { "Id", "ScopeType", "ScopeId" }));

        Assert.NotNull(foreignKey);
    }

    [Fact]
    public void AccessRole_HasUniqueIndex_ScopeType_ScopeId_NormalizedName()
    {
        var model = BuildModel();

        var role = model.FindEntityType("CoreKit.Access.Entities.AccessRole");
        Assert.NotNull(role);

        var index = role.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 3 &&
                i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ScopeType", "ScopeId", "NormalizedName" }) &&
                i.IsUnique);

        Assert.NotNull(index);
    }

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        using var db = new AccessDbContext(options);
        return db.GetService<IModel>();
    }
}
