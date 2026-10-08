namespace CoreKit.Tenant.Tests;

public sealed class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ShopDbContext> _options;
    private readonly CurrentTenant _tenant = new();
    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();

    public TenantIsolationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlite(_connection).Options;

        using (var setup = NewDb())
            setup.Database.EnsureCreated();

        Seed(_a, "a-1", "a-2");
        Seed(_b, "b-1");
    }

    public void Dispose() => _connection.Dispose();

    private ShopDbContext NewDb() => new(_options, _tenant);

    private void Seed(Guid tenantId, params string[] names)
    {
        using var scope = _tenant.Change(tenantId);
        using var db = NewDb();

        foreach (var name in names)
            db.Products.Add(new Product { Name = name });

        db.SaveChanges();
    }

    [Fact]
    public void Seeding_StampsTheTenantId()
    {
        using var db = NewDb();

        var products = db.Products.IgnoreQueryFilters().ToList();

        Assert.Equal(2, products.Count(p => p.TenantId == _a));
        Assert.Equal(1, products.Count(p => p.TenantId == _b));
    }

    [Fact]
    public void Reads_AreLimitedToTheCurrentTenant()
    {
        using var scope = _tenant.Change(_a);
        using var db = NewDb();

        Assert.Equal(new[] { "a-1", "a-2" }, db.Products.Select(p => p.Name).OrderBy(n => n));
        Assert.Equal(2, db.Products.Count());
    }

    [Fact]
    public void Reads_ReturnNothingWhenNoTenantIsResolved()
    {
        using var db = NewDb();

        Assert.Empty(db.Products.ToList());
        Assert.Equal(0, db.Products.Count());
    }

    [Fact]
    public void Reads_FollowTheTenantOfTheMoment_OnTheSameContext()
    {
        using var db = NewDb();

        using (_tenant.Change(_a))
            Assert.Equal(2, db.Products.Count());

        using (_tenant.Change(_b))
            Assert.Equal(1, db.Products.Count());

        Assert.Equal(0, db.Products.Count());
    }

    [Fact]
    public void AnotherTenantsRowCannotBeFoundById()
    {
        Guid foreignId;

        using (var all = NewDb())
            foreignId = all.Products.IgnoreQueryFilters().Single(p => p.Name == "b-1").Id;

        using var scope = _tenant.Change(_a);
        using var db = NewDb();

        Assert.Null(db.Products.FirstOrDefault(p => p.Id == foreignId));
    }

    [Fact]
    public void IgnoreQueryFilters_IsTheDeliberateWayToReadAcrossTenants()
    {
        using var scope = _tenant.Change(_a);
        using var db = NewDb();

        Assert.Equal(3, db.Products.IgnoreQueryFilters().Count());
    }

    [Fact]
    public void Insert_FillsInTheCurrentTenant()
    {
        using var scope = _tenant.Change(_b);
        using var db = NewDb();
        var product = new Product { Name = "b-2" };

        db.Products.Add(product);
        db.SaveChanges();

        Assert.Equal(_b, product.TenantId);
    }

    [Fact]
    public void Insert_ForAnotherTenantIsRefused()
    {
        using var scope = _tenant.Change(_a);
        using var db = NewDb();
        db.Products.Add(new Product { Name = "sneaky", TenantId = _b });

        Assert.Throws<TenantIsolationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Insert_WithoutATenantIsRefused()
    {
        using var db = NewDb();
        db.Products.Add(new Product { Name = "orphan" });

        Assert.Throws<TenantNotResolvedException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task SaveChangesAsync_EnforcesTheSameRules()
    {
        using var scope = _tenant.Change(_a);
        using var db = NewDb();
        db.Products.Add(new Product { Name = "sneaky", TenantId = _b });

        await Assert.ThrowsAsync<TenantIsolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Update_OfYourOwnRowWorks()
    {
        using (var scope = _tenant.Change(_a))
        using (var db = NewDb())
        {
            db.Products.First(p => p.Name == "a-1").Name = "renamed";
            db.SaveChanges();
        }

        using var check = NewDb();
        Assert.Contains(check.Products.IgnoreQueryFilters(), p => p.Name == "renamed" && p.TenantId == _a);
    }

    [Fact]
    public void Update_OfAnotherTenantsRowIsRefused()
    {
        using var scope = _tenant.Change(_a);
        using var db = NewDb();
        var foreign = db.Products.IgnoreQueryFilters().Single(p => p.TenantId == _b);

        foreign.Name = "hacked";

        Assert.Throws<TenantIsolationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Delete_OfAnotherTenantsRowIsRefused()
    {
        using var scope = _tenant.Change(_a);
        using var db = NewDb();
        var foreign = db.Products.IgnoreQueryFilters().Single(p => p.TenantId == _b);

        db.Products.Remove(foreign);

        Assert.Throws<TenantIsolationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Delete_OfYourOwnRowWorks()
    {
        using (var scope = _tenant.Change(_a))
        using (var db = NewDb())
        {
            db.Products.Remove(db.Products.First(p => p.Name == "a-1"));
            db.SaveChanges();
        }

        using var check = NewDb();
        Assert.Equal(2, check.Products.IgnoreQueryFilters().Count());
    }

    [Fact]
    public void MovingARowToAnotherTenantIsRefused()
    {
        using var scope = _tenant.Change(_a);
        using var db = NewDb();

        db.Products.First().TenantId = _b;

        Assert.Throws<TenantIsolationException>(() => db.SaveChanges());
    }

    [Fact]
    public void ActingAsAnotherTenant_IsTheDeliberateWayToWriteForIt()
    {
        using (var scope = _tenant.Change(_b))
        using (var db = NewDb())
        {
            db.Products.Add(new Product { Name = "b-2" });
            db.SaveChanges();
        }

        using var check = NewDb();
        Assert.Equal(2, check.Products.IgnoreQueryFilters().Count(p => p.TenantId == _b));
    }
}