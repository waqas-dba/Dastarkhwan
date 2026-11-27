using CoreKit.Access.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoreKit.Access.Persistence;

/// <summary>Roles, their permissions, who holds them, the audit trail, and platform membership.</summary>
public sealed class AccessDbContext : DbContext
{
    public AccessDbContext(DbContextOptions<AccessDbContext> options) : base(options)
    {
    }

    public DbSet<AccessRole> Roles => Set<AccessRole>();

    public DbSet<AccessRolePermission> RolePermissions => Set<AccessRolePermission>();

    public DbSet<AccessAssignment> Assignments => Set<AccessAssignment>();

    public DbSet<AccessAuditEntry> AuditEntries => Set<AccessAuditEntry>();

    public DbSet<AccessPlatformMember> PlatformMembers => Set<AccessPlatformMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AccessDbContextConfigurator.Schema);
        
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

            // Alternate key to support composite foreign key from AccessAssignment.
            b.HasAlternateKey(x => new { x.Id, x.ScopeType, x.ScopeId });

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
            b.Property(x => x.MembershipGeneration).IsRequired();
            b.HasIndex(x => new { x.ScopeType, x.ScopeId, x.UserId });
            b.HasIndex(x => new { x.RoleId, x.ScopeType, x.ScopeId });

            // Prevent duplicate assignment of the same role to the same user in the same scope.
            // RoleId is globally unique, but this makes the constraint explicit at the scope level.
            b.HasIndex(x => new { x.ScopeType, x.ScopeId, x.UserId, x.RoleId })
                .IsUnique().HasDatabaseName("UX_ACCESS_Assignments_Scope_User_Role");

            b.HasOne(x => x.Role).WithMany()
                .HasForeignKey(x => new { x.RoleId, x.ScopeType, x.ScopeId })
                .HasPrincipalKey(x => new { x.Id, x.ScopeType, x.ScopeId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccessPlatformMember>(b =>
        {
            b.ToTable("ACCESS_PlatformMembers");
            b.HasKey(x => new { x.PlatformId, x.UserId });
            b.Property(x => x.PlatformId).IsRequired();
            b.Property(x => x.MembershipGeneration).IsRequired();
            b.Property(x => x.JoinedAt).IsRequired();
        });

        modelBuilder.Entity<AccessAuditEntry>(b =>
        {
            b.ToTable("ACCESS_AuditEntries");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ScopeType).IsRequired().HasMaxLength(AccessScope.MaxTypeLength);
            b.Property(x => x.Action).IsRequired().HasMaxLength(100);
            b.Property(x => x.ActorType).IsRequired().HasMaxLength(30);
            b.Property(x => x.ActorSource).HasMaxLength(200);
            b.Property(x => x.Details).HasMaxLength(2000);
            b.HasIndex(x => new { x.ScopeType, x.ScopeId, x.OccurredAt });

            // Persistence-level enforcement of the permitted actor combinations. Mirrors
            // AccessAuditEntry.ValidateActor so the database is the final backstop even when
            // entity metadata is written outside the audit recorder.
            b.HasCheckConstraint(
                "CK_ACCESS_AuditEntries_Actor",
                $@"(""ActorType"" = 'User' AND ""ActorUserId"" IS NOT NULL AND CAST(""ActorUserId"" AS VARCHAR(36)) <> '00000000-0000-0000-0000-000000000000' AND ""ActorSource"" IS NULL)
    OR (""ActorType"" = 'System' AND ""ActorUserId"" IS NULL AND ""ActorSource"" IS NOT NULL AND length(trim(""ActorSource"", chr(9)||chr(10)||chr(13)||' ')) > 0)
    OR (""ActorType"" = 'Unknown' AND ""ActorUserId"" IS NULL AND ""ActorSource"" IS NULL)");
        });
    }
}