namespace CoreKit.Access.Tests;

public sealed class AccessMembershipProjectionPersistenceTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    [Fact]
    public void Projection_RoundTripsStatusGenerationSequenceAndTimestamp()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var updatedAt = new DateTime(2026, 11, 15, 14, 0, 0, DateTimeKind.Utc);

        _env.Seed(db => db.MembershipProjections.Add(new AccessMembershipProjection
        {
            TenantId = tenantId,
            UserId = userId,
            Status = AccessMembershipProjectionStatus.Active,
            MembershipGeneration = 3,
            LastAppliedSequence = 42,
            UpdatedAt = updatedAt
        }));

        var stored = _env.Read(db => db.MembershipProjections
            .Single(x => x.TenantId == tenantId && x.UserId == userId));

        Assert.Equal(AccessMembershipProjectionStatus.Active, stored.Status);
        Assert.Equal(3, stored.MembershipGeneration);
        Assert.Equal(42, stored.LastAppliedSequence);
        Assert.Equal(updatedAt, stored.UpdatedAt);
    }

    [Fact]
    public void Projection_CompositeKeyIsUniquePerTenantAndUser()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = _env.UtcNow;

        _env.Seed(db => db.MembershipProjections.Add(new AccessMembershipProjection
        {
            TenantId = tenantId,
            UserId = userId,
            Status = AccessMembershipProjectionStatus.Active,
            MembershipGeneration = 1,
            LastAppliedSequence = 1,
            UpdatedAt = now
        }));

        Assert.Throws<DbUpdateException>(() => _env.Seed(db => db.MembershipProjections.Add(new AccessMembershipProjection
        {
            TenantId = tenantId,
            UserId = userId,
            Status = AccessMembershipProjectionStatus.Removed,
            MembershipGeneration = 2,
            LastAppliedSequence = 2,
            UpdatedAt = now.AddMinutes(1)
        })));
    }

    [Fact]
    public async Task Repository_AddFindAndListByTenant_PersistProjection()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var now = _env.UtcNow;

        using (var db = _env.NewContext())
        {
            var repo = new AccessMembershipProjectionRepository(db);
            repo.Add(new AccessMembershipProjection
            {
                TenantId = tenantId,
                UserId = userA,
                Status = AccessMembershipProjectionStatus.Active,
                MembershipGeneration = 1,
                LastAppliedSequence = 10,
                UpdatedAt = now
            });
            repo.Add(new AccessMembershipProjection
            {
                TenantId = tenantId,
                UserId = userB,
                Status = AccessMembershipProjectionStatus.Removed,
                MembershipGeneration = 2,
                LastAppliedSequence = 11,
                UpdatedAt = now.AddMinutes(1)
            });
            repo.Add(new AccessMembershipProjection
            {
                TenantId = otherTenantId,
                UserId = Guid.NewGuid(),
                Status = AccessMembershipProjectionStatus.Active,
                MembershipGeneration = 1,
                LastAppliedSequence = 1,
                UpdatedAt = now
            });

            await db.SaveChangesAsync();
        }

        using (var db = _env.NewContext())
        {
            var repo = new AccessMembershipProjectionRepository(db);
            var found = await repo.FindAsync(tenantId, userA);

            Assert.NotNull(found);
            Assert.Equal(AccessMembershipProjectionStatus.Active, found!.Status);
            Assert.Equal(1, found.MembershipGeneration);
            Assert.Equal(10, found.LastAppliedSequence);

            var listed = await repo.ListByTenantAsync(tenantId);
            Assert.Equal(2, listed.Count);
            Assert.All(listed, x => Assert.Equal(tenantId, x.TenantId));
        }
    }
}
