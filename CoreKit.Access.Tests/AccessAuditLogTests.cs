using CoreKit.Access.Services;
using CoreKit.Access.Tests.Support;
using CoreKit.Access.Constants;

namespace CoreKit.Access.Tests;

public sealed class AccessAuditLogTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private AccessAuditLog Svc => _env.CreateAuditLog();

    [Fact]
    public async Task ListAsync_WithoutAuditRead_IsRejected()
    {
        var actor = _env.AddMember(scope: _env.Scope);

        _env.SignInAs("sales.create");

        var result = await Svc.ListAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(AccessErrorKind.Forbidden, result.Error!.Kind);
    }

    [Fact]
    public async Task ListAsync_WithAuditRead_ReturnsRecords()
    {
        var actor = _env.AddMember(scope: _env.Scope, isOwner: true);

        _env.SignInAs(AccessPermissionNames.AuditRead);

        _env.Seed(db => db.AuditEntries.Add(new AccessAuditEntry
        {
            Id = Guid.NewGuid(),
            ScopeType = _env.Scope.Type,
            ScopeId = _env.Scope.Id,
            Action = "role.created",
            ActorUserId = actor,
            Details = "test action",
            OccurredAt = _env.UtcNow
        }));

        var result = ResultAssert.Ok(await Svc.ListAsync());

        Assert.Single(result.Items);
        Assert.Equal("role.created", result.Items[0].Action);
    }

    [Fact]
    public async Task ListAsync_DoesNotReturnOtherScopeRecords()
    {
        var actor = _env.AddMember(scope: _env.Scope, isOwner: true);
        var otherScope = AccessScope.ForTenant(Guid.NewGuid());

        _env.SignInAs(AccessPermissionNames.AuditRead);

        _env.Seed(db => db.AuditEntries.Add(new AccessAuditEntry
        {
            Id = Guid.NewGuid(),
            ScopeType = _env.Scope.Type,
            ScopeId = _env.Scope.Id,
            Action = "role.created",
            ActorUserId = actor,
            Details = "in scope",
            OccurredAt = _env.UtcNow
        }));

        _env.Seed(db => db.AuditEntries.Add(new AccessAuditEntry
        {
            Id = Guid.NewGuid(),
            ScopeType = otherScope.Type,
            ScopeId = otherScope.Id,
            Action = "role.created",
            ActorUserId = actor,
            Details = "other scope",
            OccurredAt = _env.UtcNow
        }));

        var result = ResultAssert.Ok(await Svc.ListAsync());

        Assert.Single(result.Items);
        Assert.Equal("in scope", result.Items[0].Details);
    }

    [Fact]
    public async Task ListAsync_PageNormalization()
    {
        var actor = _env.AddMember(scope: _env.Scope, isOwner: true);

        _env.SignInAs(AccessPermissionNames.AuditRead);

        for (int i = 0; i < 5; i++)
        {
            _env.Seed(db => db.AuditEntries.Add(new AccessAuditEntry
            {
                Id = Guid.NewGuid(),
                ScopeType = _env.Scope.Type,
                ScopeId = _env.Scope.Id,
                Action = "role.created",
                ActorUserId = actor,
                Details = $"entry {i}",
                OccurredAt = _env.UtcNow.AddSeconds(i)
            }));
        }

        var page0 = ResultAssert.Ok(await Svc.ListAsync(page: 0, pageSize: 10));
        Assert.Equal(1, page0.Page);
        Assert.Equal(5, page0.Items.Count);

        var pageSize0 = ResultAssert.Ok(await Svc.ListAsync(page: 1, pageSize: 0));
        Assert.Equal(1, pageSize0.PageSize);
        Assert.Equal(1, pageSize0.Items.Count);

        var pageSizeLarge = ResultAssert.Ok(await Svc.ListAsync(page: 1, pageSize: 999));
        Assert.Equal(200, pageSizeLarge.PageSize);
        Assert.Equal(5, pageSizeLarge.Items.Count);
    }
}
