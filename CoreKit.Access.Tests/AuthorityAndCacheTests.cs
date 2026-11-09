using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace CoreKit.Access.Tests;

/// <summary>
/// Changing roles or assignments is decided on a person's permissions right now, not on a remembered copy.
/// </summary>
public sealed class FreshAuthorityTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task ManagingRoles_UsesCurrentAuthority_NotTheCache()
    {
        var manager = _env.SignInAs(AccessPermissionNames.RolesManage, "sales.create");

        // The manager's permissions are now remembered by the cache.
        Assert.True((await _env.Access.GetAccessAsync(manager, _env.Scope)).Has(AccessPermissionNames.RolesManage));

        // Their role is withdrawn directly in the database, so the cache has not heard about it.
        _env.Seed(db => db.Assignments.RemoveRange(db.Assignments.Where(a => a.UserId == manager)));

        var result = await _env.CreateRoleService().CreateAsync(new CreateAccessRoleRequest { Name = "Sneaky" });

        ResultAssert.Fails(result, AccessErrorKind.Forbidden, AccessErrors.Denied.Code);
        Assert.Empty(_env.Read(db => db.Roles.Where(r => r.Name == "Sneaky").ToList()));
    }

    [Fact]
    public async Task ManagingAssignments_UsesCurrentAuthority_NotTheCache()
    {
        var manager = _env.SignInAs(AccessPermissionNames.AssignmentsManage, "sales.create");
        var cashier = _env.AddRole("Cashier", new[] { "sales.create" });
        var staff = _env.AddMember();

        Assert.True((await _env.Access.GetAccessAsync(manager, _env.Scope)).Has(AccessPermissionNames.AssignmentsManage));

        _env.Seed(db => db.Assignments.RemoveRange(db.Assignments.Where(a => a.UserId == manager)));

        var result = await _env.CreateAssignmentService().SetRolesAsync(
            staff, new SetAccessUserRolesRequest { RoleIds = new List<Guid> { cashier.Id } });

        ResultAssert.Fails(result, AccessErrorKind.Forbidden, AccessErrors.Denied.Code);
        Assert.Empty(_env.Read(db => db.Assignments.Where(a => a.UserId == staff).ToList()));
    }

    [Fact]
    public async Task ReadingRoles_StillUsesTheCache()
    {
        _env.SignInAs(AccessPermissionNames.RolesRead);
        var service = _env.CreateRoleService();

        ResultAssert.Ok(await service.ListAsync());
        ResultAssert.Ok(await service.ListAsync());

        Assert.Equal(1, _env.Membership.Lookups);
    }
}

/// <summary>
/// A lookup that started before a role or assignment changed must not store its (stale) result.
/// </summary>
public sealed class AccessCacheStaleResultTests
{
    private readonly MemoryCache _memory = new(new MemoryCacheOptions());

    private AccessCache Cache() => new(_memory, Options.Create(new AccessOptions { CacheSeconds = 30 }));

    private static readonly AccessScope Scope = AccessScope.ForTenant(Guid.NewGuid());

    private static EffectiveAccess Access(params string[] permissions)
        => new(true, false, permissions.ToHashSet(), Array.Empty<string>());

    [Fact]
    public void ResultIsStored_WhenNothingChangedDuringTheLookup()
    {
        var cache = Cache();
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.Set(Scope, user, Access("a.b"), token);

        Assert.True(cache.TryGet(Scope, user, out var found));
        Assert.True(found.Has("a.b"));
    }

    [Fact]
    public void ResultIsNotStored_WhenTheScopeWasInvalidatedDuringTheLookup()
    {
        var cache = Cache();
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.InvalidateScope(Scope);
        cache.Set(Scope, user, Access("old.rights"), token);

        Assert.False(cache.TryGet(Scope, user, out _));
    }

    [Fact]
    public void ResultIsNotStored_WhenTheUserWasInvalidatedDuringTheLookup()
    {
        var cache = Cache();
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.InvalidateUser(Scope, user);
        cache.Set(Scope, user, Access("old.rights"), token);

        Assert.False(cache.TryGet(Scope, user, out _));
    }

    [Fact]
    public void InvalidatingOneUser_DoesNotBlockStoringForAnotherUser()
    {
        var cache = Cache();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var token = cache.Begin(Scope, second);
        cache.InvalidateUser(Scope, first);
        cache.Set(Scope, second, Access("a.b"), token);

        Assert.True(cache.TryGet(Scope, second, out _));
    }
}

public sealed class AccessAuditDetailTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task AuditDetails_HaveNoLineBreaksOrControlCharacters()
    {
        _env.Audit.Record(_env.Scope, "access.test", "line1\nline2\r\u0007end");

        Assert.Equal(SaveOutcome.Saved, await _env.UnitOfWork.SaveAsync());

        var entry = _env.Read(db => db.AuditEntries.Single());
        Assert.Equal("line1 line2  end", entry.Details);
    }

    [Fact]
    public async Task SetRoles_RecordsWhatWasAddedAndWhatWasRemoved()
    {
        _env.SignInOwner();
        var cashier = _env.AddRole("Cashier", new[] { "sales.create" });
        var stock = _env.AddRole("Stock", new[] { "products.manage" });
        var staff = _env.AddMember(roles: new[] { cashier });

        ResultAssert.Ok(await _env.CreateAssignmentService().SetRolesAsync(
            staff, new SetAccessUserRolesRequest { RoleIds = new List<Guid> { stock.Id } }));

        var entry = _env.Read(db => db.AuditEntries.Single(a => a.Action == AccessAuditActions.RolesAssigned));
        Assert.Contains("added: Stock", entry.Details);
        Assert.Contains("removed: Cashier", entry.Details);
    }
}

/// <summary>
/// Generation-counter bounded, lazy cache behavior with rollover and concurrency safety.
/// </summary>
public sealed class AccessCacheGenerationMetadataTests
{
    private AccessCache Cache(int maxGenerationEntries = 10_000, int cacheSeconds = 30)
        => new(new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new AccessOptions { CacheSeconds = cacheSeconds }),
            maxGenerationEntries);

    private static readonly AccessScope Scope = AccessScope.ForTenant(Guid.NewGuid());

    private static EffectiveAccess Access(params string[] permissions)
        => new(true, false, permissions.ToHashSet(), Array.Empty<string>());

    [Fact]
    public void OrdinaryLookupDoesNotAllocateGenerationMetadata()
    {
        var cache = Cache(2);
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.Set(Scope, user, Access("a.b"), token);
        cache.TryGet(Scope, user, out _);

        // Ordinary lookups must not create generation dictionary entries.
        Assert.Equal(0, cache.GenerationEntryCount);
    }

    [Fact]
    public void ScopeInvalidationPreventsStaleReads()
    {
        var cache = Cache();
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.Set(Scope, user, Access("a.b"), token);
        cache.InvalidateScope(Scope);

        Assert.False(cache.TryGet(Scope, user, out _));
    }

    [Fact]
    public void UserInvalidationPreventsStaleReads()
    {
        var cache = Cache();
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.Set(Scope, user, Access("a.b"), token);
        cache.InvalidateUser(Scope, user);

        Assert.False(cache.TryGet(Scope, user, out _));
    }

    [Fact]
    public void StaleInFlightLookupCannotStoreAfterInvalidation()
    {
        var cache = Cache();
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.InvalidateScope(Scope);
        cache.Set(Scope, user, Access("stale"), token);

        Assert.False(cache.TryGet(Scope, user, out _));
    }

    [Fact]
    public void OtherUsersCanStoreAfterOneUserInvalidated()
    {
        var cache = Cache();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var token = cache.Begin(Scope, second);
        cache.InvalidateUser(Scope, first);
        cache.Set(Scope, second, Access("a.b"), token);

        Assert.True(cache.TryGet(Scope, second, out _));
    }

    [Fact]
    public void CapacityBoundaryWithInjectedCapacity_TriggersRollover()
    {
        var cache = Cache(2);
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        // Two invalidations fill the capacity.
        cache.InvalidateScope(Scope);
        cache.InvalidateUser(Scope, user1);
        Assert.Equal(2, cache.GenerationEntryCount);

        // A third invalidation forces rollover.
        cache.InvalidateUser(Scope, user2);

        // After rollover, old metadata is cleared.
        Assert.True(cache.GenerationEntryCount <= 2);

        // New entries work after rollover.
        var token = cache.Begin(Scope, user3);
        cache.Set(Scope, user3, Access("a.b"), token);
        Assert.True(cache.TryGet(Scope, user3, out var found));
        Assert.True(found.Has("a.b"));
    }

    [Fact]
    public void RolloverInvalidatesPreviousCacheEntries()
    {
        var cache = Cache(1);
        var user = Guid.NewGuid();

        // Store an entry.
        var token = cache.Begin(Scope, user);
        cache.Set(Scope, user, Access("old"), token);
        Assert.True(cache.TryGet(Scope, user, out var before));
        Assert.True(before.Has("old"));

        // First invalidation fills the capacity.
        cache.InvalidateScope(Scope);
        Assert.Equal(1, cache.GenerationEntryCount);

        // Second invalidation of a different scope triggers rollover (count = 1 >= capacity = 1).
        var other = AccessScope.ForTenant(Guid.NewGuid());
        cache.InvalidateScope(other);

        // Old entry is unreachable because epoch changed.
        Assert.False(cache.TryGet(Scope, user, out _));
    }

    [Fact]
    public void RolloverRejectsTokensFromPreviousEpoch()
    {
        var cache = Cache(1);
        var user = Guid.NewGuid();

        // First invalidation fills the capacity.
        cache.InvalidateScope(Scope);
        Assert.Equal(1, cache.GenerationEntryCount);

        // Get a token before rollover.
        var oldToken = cache.Begin(Scope, user);

        // Trigger rollover by invalidating a different scope.
        var other = AccessScope.ForTenant(Guid.NewGuid());
        cache.InvalidateScope(other);

        // Old token cannot store after rollover.
        cache.Set(Scope, user, Access("stale"), oldToken);
        Assert.False(cache.TryGet(Scope, user, out _));

        // New token works.
        var newToken = cache.Begin(Scope, user);
        cache.Set(Scope, user, Access("fresh"), newToken);
        Assert.True(cache.TryGet(Scope, user, out var found));
        Assert.True(found.Has("fresh"));
    }

    [Fact]
    public void MetadataRemainsWithinCapacity()
    {
        var cache = Cache(5);
        var scope = Scope;

        // Repeated invalidations of different users.
        for (int i = 0; i < 100; i++)
        {
            cache.InvalidateUser(scope, Guid.NewGuid());
        }

        Assert.True(cache.GenerationEntryCount <= 5);
    }

    [Fact]
    public void DisabledCachingCreatesNoGenerationState()
    {
        var cache = new AccessCache(new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new AccessOptions { CacheSeconds = 0 }), 10_000);
        var user = Guid.NewGuid();

        var token = cache.Begin(Scope, user);
        cache.Set(Scope, user, Access("a.b"), token);
        cache.InvalidateScope(Scope);
        cache.InvalidateUser(Scope, user);

        Assert.Equal(0, cache.GenerationEntryCount);
        Assert.False(cache.TryGet(Scope, user, out _));
    }
}

/// <summary>
/// Concurrency safety for the generation-counter cache.
/// </summary>
public sealed class AccessCacheConcurrencyTests
{
    private AccessCache Cache(int maxGenerationEntries = 10_000, int cacheSeconds = 30)
        => new(new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new AccessOptions { CacheSeconds = cacheSeconds }),
            maxGenerationEntries);

    private static readonly AccessScope Scope = AccessScope.ForTenant(Guid.NewGuid());

    private static EffectiveAccess Access(params string[] permissions)
        => new(true, false, permissions.ToHashSet(), Array.Empty<string>());

    [Fact]
    public void ConcurrencyInvalidationOrdering_IsDeterministic()
    {
        var cache = Cache();
        var user = Guid.NewGuid();

        // Store an entry.
        var token = cache.Begin(Scope, user);
        cache.Set(Scope, user, Access("a.b"), token);
        Assert.True(cache.TryGet(Scope, user, out var before));
        Assert.True(before.Has("a.b"));

        // Invalidate synchronously.
        cache.InvalidateScope(Scope);

        // Old token cannot store after invalidation.
        cache.Set(Scope, user, Access("stale"), token);
        Assert.False(cache.TryGet(Scope, user, out _));

        // New token works.
        var newToken = cache.Begin(Scope, user);
        cache.Set(Scope, user, Access("fresh"), newToken);
        Assert.True(cache.TryGet(Scope, user, out var after));
        Assert.True(after.Has("fresh"));
    }

    [Fact]
    public void ConcurrencyStressTest_VerifiesInvariants()
    {
        var cache = Cache(50);
        var scope = Scope;
        var users = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var exceptions = new ConcurrentBag<Exception>();

        // Pre-populate entries.
        foreach (var user in users)
        {
            var populateToken = cache.Begin(scope, user);
            cache.Set(scope, user, Access("initial"), populateToken);
        }

        // Multiple threads concurrently invalidate and store.
        var tasks = new Task[30];
        for (int i = 0; i < tasks.Length; i++)
        {
            int userIndex = i % users.Length;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 50; j++)
                    {
                        var taskToken = cache.Begin(scope, users[userIndex]);
                        if (j % 3 == 0)
                            cache.InvalidateScope(scope);
                        else if (j % 3 == 1)
                            cache.InvalidateUser(scope, users[userIndex]);
                        else
                            cache.Set(scope, users[userIndex], Access("stress"), taskToken);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

#pragma warning disable xUnit1031 // Blocking wait is intentional for concurrent stress testing.
        Task.WaitAll(tasks);
#pragma warning restore xUnit1031

        // No exceptions from concurrent access.
        Assert.Empty(exceptions);

        // Cache still works after stress.
        var token = cache.Begin(scope, users[0]);
        cache.Set(scope, users[0], Access("final"), token);
        Assert.True(cache.TryGet(scope, users[0], out var found));
        Assert.True(found.Has("final"));
    }
}
