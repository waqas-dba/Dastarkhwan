using CoreKit.Architecture.Tests.Support;
using Microsoft.Extensions.Caching.Memory;

namespace CoreKit.Access.Tests.Support;

/// <summary>
/// One real object graph on an in-memory SQLite database, built like a single web request.
/// Membership, the current user and the scope are fakes the test controls. Seed with one context and read
/// back with another, so nothing is served from a tracker.
/// </summary>
internal sealed class TestEnv : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AccessDbContext> _dbOptions;
    private readonly MemoryCache _memory = new(new MemoryCacheOptions());

    public TestEnv(AccessOptions? options = null, Action<AccessCatalogBuilder>? catalog = null)
    {
        Config = options ?? new AccessOptions();

        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<AccessDbContext>().UseSqlite(_connection).Options;

        using (var setup = NewContext())
            setup.Database.EnsureCreated();

        Db = NewContext();
        Time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));
        Scope = AccessScope.ForTenant(Guid.NewGuid());
        ScopeContext = new FakeScopeContext { Current = Scope };
        Membership = new FakeMembership();
        User = new FakeCurrentUser();
        Catalog = AccessCatalog.Build(new[] { new AccessCatalogContribution(catalog ?? DefaultCatalog) });
        Cache = new AccessCache(_memory, Options.Create(Config));

        Roles = new AccessRoleRepository(Db);
        Assignments = new AccessAssignmentRepository(Db);
        AuditRepo = new AccessAuditRepository(Db);
        UnitOfWork = new AccessUnitOfWork(Db, NullLogger<AccessUnitOfWork>.Instance);
        Audit = new AccessAuditRecorder(AuditRepo, User, Time);
        Access = new AccessService(Membership, Assignments, Cache, Catalog, ScopeContext, User);
        Guard = new AccessGuard(ScopeContext, User, Access);
    }

    public static void DefaultCatalog(AccessCatalogBuilder b)
    {
        b.Add("sales.create", "Ring up a sale")
         .Add("sales.refund", "Refund a sale")
         .Add("products.view", "See products")
         .Add("products.manage", "Create and edit products");

        b.AddRoleTemplate(
            "Manager", "Runs the shop",
            "sales.create", "sales.refund", "products.view", "products.manage",
            AccessPermissionNames.RolesRead, AccessPermissionNames.AssignmentsRead, AccessPermissionNames.AssignmentsManage);

        b.AddRoleTemplate("Cashier", "Rings up sales", "sales.create", "products.view");
    }

    public AccessOptions Config { get; }

    public AccessDbContext Db { get; }

    public FixedTimeProvider Time { get; }

    public AccessScope Scope { get; }

    public FakeScopeContext ScopeContext { get; }

    public FakeMembership Membership { get; }

    public FakeCurrentUser User { get; }

    public AccessCatalog Catalog { get; }

    public AccessCache Cache { get; }

    public AccessRoleRepository Roles { get; }

    public AccessAssignmentRepository Assignments { get; }

    public AccessAuditRepository AuditRepo { get; }

    public AccessUnitOfWork UnitOfWork { get; }

    public AccessAuditRecorder Audit { get; }

    public AccessService Access { get; }

    public AccessGuard Guard { get; }

    public DateTime UtcNow => Time.GetUtcNow().UtcDateTime;

    public AccessDbContext NewContext() => new(_dbOptions);

    public AccessRoleService CreateRoleService() => new(
        Guard, Roles, Assignments, Audit, UnitOfWork, Cache, Catalog, Options.Create(Config), Time);

    public AccessAssignmentService CreateAssignmentService() => new(
        Guard, Membership, Roles, Assignments, Audit, UnitOfWork, Cache, Options.Create(Config), Time);

    public AccessMaintenance CreateMaintenance() => new(Roles, Assignments, Audit, UnitOfWork, Cache, Catalog, Time);

    public AccessAuditLog CreateAuditLog() => new(Guard, AuditRepo);

    // ---- seeding and reading ----

    public void Seed(Action<AccessDbContext> action)
    {
        using var db = NewContext();
        action(db);
        db.SaveChanges();
    }

    public T Read<T>(Func<AccessDbContext, T> query)
    {
        using var db = NewContext();
        return query(db);
    }

    public AccessRole AddRole(string name, string[]? permissions = null, AccessScope? scope = null)
    {
        var s = scope ?? Scope;

        var role = new AccessRole
        {
            Id = Guid.NewGuid(),
            ScopeType = s.Type,
            ScopeId = s.Id,
            Name = name,
            NormalizedName = AccessValidation.NormalizeName(name),
            CreatedAt = UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        foreach (var permission in permissions ?? Array.Empty<string>())
            role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, Permission = permission });

        Seed(db => db.Roles.Add(role));
        return role;
    }

    public void Assign(Guid userId, AccessRole role)
        => Seed(db => db.Assignments.Add(new AccessAssignment
        {
            RoleId = role.Id,
            UserId = userId,
            ScopeType = role.ScopeType,
            ScopeId = role.ScopeId,
            AssignedAt = UtcNow
        }));

    /// <summary>A person who belongs to the scope, optionally an owner, optionally holding roles.</summary>
    public Guid AddMember(bool isOwner = false, AccessRole[]? roles = null, AccessScope? scope = null)
    {
        var id = Guid.NewGuid();

        Membership.Set(scope ?? Scope, id, isOwner);

        foreach (var role in roles ?? Array.Empty<AccessRole>())
            Assign(id, role);

        return id;
    }

    public Guid SignIn(Guid userId)
    {
        User.UserId = userId;
        return userId;
    }

    public Guid SignInOwner() => SignIn(AddMember(isOwner: true));

    /// <summary>Signs in a member whose only role holds exactly these permissions.</summary>
    public Guid SignInAs(params string[] permissions)
    {
        var role = AddRole("Test role " + Guid.NewGuid().ToString("N")[..8], permissions);
        return SignIn(AddMember(roles: new[] { role }));
    }

    public void Dispose()
    {
        Db.Dispose();
        _memory.Dispose();
        _connection.Dispose();
    }
}