using System.Reflection.Emit;

namespace CoreKit.Tenant.Persistence;

/// <summary>
/// The tenant registry: tenants, their settings and members, and the audit trail.
/// It is a platform-level store, so it is not filtered by tenant. The services enforce the tenant boundary.
/// </summary>
public sealed class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options)
    {
    }

    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();

    public DbSet<TenantSetting> Settings => Set<TenantSetting>();

    public DbSet<TenantMember> Members => Set<TenantMember>();

    public DbSet<TenantAuditEntry> AuditEntries => Set<TenantAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantEntity>(b =>
        {
            b.ToTable("TENANT_Tenants");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Slug).IsRequired().HasMaxLength(63);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.StatusReason).HasMaxLength(500);
            b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();

            // The database itself refuses a second tenant with the same slug, even if two requests race.
            b.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("UX_TENANT_Tenants_Slug");
            b.HasIndex(x => x.Status);

            b.HasMany(x => x.Settings).WithOne(x => x.Tenant)
                .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.Members).WithOne(x => x.Tenant)
                .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantSetting>(b =>
        {
            b.ToTable("TENANT_Settings");
            b.HasKey(x => new { x.TenantId, x.Key });
            b.Property(x => x.Key).IsRequired().HasMaxLength(100);
            b.Property(x => x.Value).IsRequired().HasMaxLength(4000);
        });

        modelBuilder.Entity<TenantMember>(b =>
        {
            b.ToTable("TENANT_Members");
            b.HasKey(x => new { x.TenantId, x.UserId });
            b.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<TenantAuditEntry>(b =>
        {
            b.ToTable("TENANT_AuditEntries");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Action).IsRequired().HasMaxLength(100);
            b.Property(x => x.Details).HasMaxLength(2000);
            b.HasIndex(x => new { x.TenantId, x.OccurredAt });
        });
    }
}