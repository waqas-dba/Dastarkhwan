namespace CoreKit.Access.Persistence;

/// <summary>Roles, their permissions, who holds them, and the audit trail. Independent of every other module.</summary>
public sealed class AccessDbContext : DbContext
{
    public AccessDbContext(DbContextOptions<AccessDbContext> options) : base(options)
    {
    }

    public DbSet<AccessRole> Roles => Set<AccessRole>();

    public DbSet<AccessRolePermission> RolePermissions => Set<AccessRolePermission>();

    public DbSet<AccessAssignment> Assignments => Set<AccessAssignment>();

    public DbSet<AccessAuditEntry> AuditEntries => Set<AccessAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccessRole>(b =>
        {
            b.ToTable("ACCESS_Roles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ScopeType).IsRequired().HasMaxLength(AccessScope.MaxTypeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.NormalizedName).IsRequired().HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();

            // The database itself refuses a second role with the same name in a scope, even if two requests race.
            b.HasIndex(x => new { x.ScopeType, x.ScopeId, x.NormalizedName })
                .IsUnique().HasDatabaseName("UX_ACCESS_Roles_Scope_Name");

            b.HasMany(x => x.Permissions).WithOne(x => x.Role)
                .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessRolePermission>(b =>
        {
            b.ToTable("ACCESS_RolePermissions");
            b.HasKey(x => new { x.RoleId, x.Permission });
            b.Property(x => x.Permission).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<AccessAssignment>(b =>
        {
            b.ToTable("ACCESS_Assignments");
            b.HasKey(x => new { x.RoleId, x.UserId });
            b.Property(x => x.ScopeType).IsRequired().HasMaxLength(AccessScope.MaxTypeLength);
            b.HasIndex(x => new { x.ScopeType, x.ScopeId, x.UserId });

            b.HasOne(x => x.Role).WithMany()
                .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessAuditEntry>(b =>
        {
            b.ToTable("ACCESS_AuditEntries");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ScopeType).IsRequired().HasMaxLength(AccessScope.MaxTypeLength);
            b.Property(x => x.Action).IsRequired().HasMaxLength(100);
            b.Property(x => x.Details).HasMaxLength(2000);
            b.HasIndex(x => new { x.ScopeType, x.ScopeId, x.OccurredAt });
        });
    }
}