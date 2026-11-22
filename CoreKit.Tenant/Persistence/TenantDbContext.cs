using System.Reflection.Emit;
using CoreKit.Tenant.Entities;

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

    public DbSet<TenantOutboxMessage> OutboxMessages => Set<TenantOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TenantDbContextConfigurator.Schema);
        
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
            b.Property(x => x.LastEventSequence).IsRequired();

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

            // Soft-deleted members are excluded from every query: a removal must end membership
            // even though the row (and its generation) is kept for Access generation enforcement.
            b.HasQueryFilter(m => m.RemovedAt == null);
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

        modelBuilder.Entity<TenantOutboxMessage>(b =>
        {
            b.ToTable("TENANT_OutboxMessages");
            b.HasKey(x => x.EventId);
            b.Property(x => x.EventId).ValueGeneratedNever();
            b.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            b.Property(x => x.PayloadJson).IsRequired().HasMaxLength(4000);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.LeaseOwner).HasMaxLength(100);
            b.Property(x => x.LastError).HasMaxLength(2000);

            // Backstop for the per-tenant ordering guarantee: two events can never share a sequence.
            b.HasIndex(x => new { x.TenantId, x.Sequence })
                .IsUnique().HasDatabaseName("UX_TENANT_OutboxMessages_Tenant_Sequence");

            // The publisher's claim scan.
            b.HasIndex(x => new { x.Status, x.NextAttemptAt })
                .HasDatabaseName("IX_TENANT_OutboxMessages_Status_NextAttempt");
        });
    }
}