namespace CoreKit.Identity.Tests;

/// <summary>
/// What this protects: who may read the permission catalogue.
/// Why it matters: the catalogue names every capability in the product, so exposing it to an
/// unprivileged caller hands them a map of what to attack and what to ask for.
/// The rule under test (see PermissionService): both listing and fetching a single permission
/// require permissions.read, and missing entries are reported as not found rather than leaked.
/// </summary>
public sealed class PermissionServiceTests : IDisposable
{
    private readonly TestEnv _env;

    public PermissionServiceTests() => _env = new TestEnv();

    public void Dispose() => _env.Dispose();

    [Fact]
    public async Task ListPermissions_AsOwner_Succeeds()
    {
        _env.SignInOwner();

        var permissions = ResultAssert.Ok(await _env.PermissionService.ListAsync());

        Assert.Contains(permissions, p => p.Name == IdentityPermissionNames.UsersRead);
    }

    [Fact]
    public async Task ListPermissions_WithoutPermissionsRead_Fails()
    {
        _env.SignInWith(_env.StaffUserId, new List<string> { IdentityRoleNames.Staff },
            IdentityPermissionNames.UsersRead);

        ResultAssert.Fails(await _env.PermissionService.ListAsync(),
            IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task GetPermission_AsOwner_ReturnsIt()
    {
        _env.SignInOwner();

        var permission = ResultAssert.Ok(await _env.PermissionService.GetAsync(_env.UserPermissionId));

        Assert.Equal(IdentityPermissionNames.UsersRead, permission.Name);
    }

    [Fact]
    public async Task GetPermission_WithoutPermissionsRead_Fails()
    {
        _env.SignInWith(_env.StaffUserId, new List<string> { IdentityRoleNames.Staff },
            IdentityPermissionNames.UsersRead);

        ResultAssert.Fails(await _env.PermissionService.GetAsync(_env.UserPermissionId),
            IdentityErrorKind.Forbidden, "access.denied");
    }

    [Fact]
    public async Task GetPermission_ThatDoesNotExist_Fails()
    {
        _env.SignInOwner();

        ResultAssert.Fails(await _env.PermissionService.GetAsync(Guid.NewGuid()),
            IdentityErrorKind.NotFound, "not_found");
    }
}
