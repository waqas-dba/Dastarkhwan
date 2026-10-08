namespace CoreKit.Tenant.Tests.Support;

/// <summary>A pretend consumer entity and context, to test the isolation a consuming app gets.</summary>
public sealed class Product : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class ShopDbContext : TenantAwareDbContext
{
    public ShopDbContext(DbContextOptions<ShopDbContext> options, ICurrentTenant currentTenant)
        : base(options, currentTenant)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Product>().HasKey(p => p.Id);
}