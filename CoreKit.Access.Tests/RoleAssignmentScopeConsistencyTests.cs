using System.IO;
using CoreKit.Access.Abstractions;
using CoreKit.Access.Entities;
using CoreKit.Access.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoreKit.Access.Tests;

/// <summary>
/// What this protects: scope consistency for Access role assignments. A role belongs to exactly
/// one scope (ScopeType + ScopeId), and an assignment must reference that same scope. The database
/// enforces this with a composite foreign key (RoleId, ScopeType, ScopeId) on AccessAssignment
/// referencing (Id, ScopeType, ScopeId) on AccessRole.
/// Why it matters: if a role from one tenant could be assigned to a user in another tenant, the
/// tenant isolation boundary would be broken.
/// The rule under test (see AccessDbContext.OnModelCreating): every assignment's RoleId + ScopeType
/// + ScopeId must match an existing role's Id + ScopeType + ScopeId in that same scope.
/// </summary>
public class RoleAssignmentScopeConsistencyTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AccessDbContext _db;

    public RoleAssignmentScopeConsistencyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"RoleAssignmentScopeConsistency_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseSqlite($"DataSource={_dbPath};Foreign Keys=True")
            .Options;
        _db = new AccessDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task AssignRole_WithinSameScope_Succeeds()
    {
        // Arrange: a role in scope A
        var scopeA = AccessScope.ForTenant(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var role = new AccessRole
        {
            Id = Guid.NewGuid(),
            ScopeType = scopeA.Type,
            ScopeId = scopeA.Id,
            Name = "Cashier",
            NormalizedName = "CASHIER",
            ConcurrencyStamp = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        var repo = new AccessAssignmentRepository(_db);
        var userId = Guid.NewGuid();

        // Act: assign the role to a user in the same scope
        repo.Add(new AccessAssignment
        {
            RoleId = role.Id,
            UserId = userId,
            ScopeType = scopeA.Type,
            ScopeId = scopeA.Id,
            AssignedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Assert: the assignment is persisted and readable back in scope A
        var saved = await repo.ListForUserForUpdateAsync(scopeA, userId);
        Assert.Single(saved);
        Assert.Equal(role.Id, saved[0].RoleId);
        Assert.Equal(scopeA.Type, saved[0].ScopeType);
        Assert.Equal(scopeA.Id, saved[0].ScopeId);
    }

    [Fact]
    public async Task AssignRole_FromDifferentScope_IsRejectedByDatabase()
    {
        // Arrange: a role in scope A
        var scopeA = AccessScope.ForTenant(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var scopeB = AccessScope.ForTenant(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var roleInScopeA = new AccessRole
        {
            Id = Guid.NewGuid(),
            ScopeType = scopeA.Type,
            ScopeId = scopeA.Id,
            Name = "Cashier",
            NormalizedName = "CASHIER",
            ConcurrencyStamp = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        _db.Roles.Add(roleInScopeA);
        await _db.SaveChangesAsync();

        var repo = new AccessAssignmentRepository(_db);
        var userId = Guid.NewGuid();

        // Act: try to assign a scope-A role to a user in scope B.
        // The composite FK on AccessAssignment (RoleId, ScopeType, ScopeId) -> AccessRole
        // (Id, ScopeType, ScopeId) requires the assignment's scope to match the role's scope.
        repo.Add(new AccessAssignment
        {
            RoleId = roleInScopeA.Id,
            UserId = userId,
            ScopeType = scopeB.Type,
            ScopeId = scopeB.Id,
            AssignedAt = DateTime.UtcNow
        });

        // Assert: the database rejects the cross-scope assignment
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task ListForUserForUpdateAsync_OnlyReturnsAssignmentsInSameScope()
    {
        // Arrange: roles and assignments in both scopes
        var scopeA = AccessScope.ForTenant(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var scopeB = AccessScope.ForTenant(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var roleInScopeA = new AccessRole
        {
            Id = Guid.NewGuid(), ScopeType = scopeA.Type, ScopeId = scopeA.Id,
            Name = "Cashier", NormalizedName = "CASHIER", ConcurrencyStamp = Guid.NewGuid(), CreatedAt = DateTime.UtcNow
        };
        var roleInScopeB = new AccessRole
        {
            Id = Guid.NewGuid(), ScopeType = scopeB.Type, ScopeId = scopeB.Id,
            Name = "Cashier", NormalizedName = "CASHIER", ConcurrencyStamp = Guid.NewGuid(), CreatedAt = DateTime.UtcNow
        };
        _db.Roles.Add(roleInScopeA);
        _db.Roles.Add(roleInScopeB);
        await _db.SaveChangesAsync();

        var repo = new AccessAssignmentRepository(_db);
        var userInScopeA = Guid.NewGuid();
        var userInScopeB = Guid.NewGuid();

        repo.Add(new AccessAssignment { RoleId = roleInScopeA.Id, UserId = userInScopeA, ScopeType = scopeA.Type, ScopeId = scopeA.Id, AssignedAt = DateTime.UtcNow });
        repo.Add(new AccessAssignment { RoleId = roleInScopeB.Id, UserId = userInScopeB, ScopeType = scopeB.Type, ScopeId = scopeB.Id, AssignedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        // Act: query assignments for userInScopeA in scope A
        var assignments = await repo.ListForUserForUpdateAsync(scopeA, userInScopeA);

        // Assert: only the assignment in scope A is returned, never scope B
        Assert.Single(assignments);
        Assert.Equal(roleInScopeA.Id, assignments[0].RoleId);
        Assert.Equal(userInScopeA, assignments[0].UserId);
        Assert.Equal(scopeA.Type, assignments[0].ScopeType);
        Assert.Equal(scopeA.Id, assignments[0].ScopeId);
    }
}
