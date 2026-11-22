namespace CoreKit.Access.Persistence;

/// <summary>Roles, their permissions, who holds them, the audit trail, and the tenant membership projection.</summary>
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

    public DbSet<AccessMembershipProjection> MembershipProjections => Set<AccessMembershipProjection>();

    public DbSet<AccessMembershipStreamCursor> MembershipStreamCursors => Set<AccessMembershipStreamCursor>();

    public DbSet<AccessProcessedEvent> ProcessedEvents => Set<AccessProcessedEvent>();

    public DbSet<AccessMembershipWatermark> MembershipWatermarks => Set<AccessMembershipWatermark>();

    public DbSet<AccessQuarantinedTenant> QuarantinedTenants => Set<AccessQuarantinedTenant>();

    public DbSet<AccessTenantState> TenantStates => Set<AccessTenantState>();

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

            b.HasOne(x => x.Role).WithMany()
                .HasForeignKey(x => new { x.RoleId, x.ScopeType, x.ScopeId })
                .HasPrincipalKey(x => new { x.Id, x.ScopeType, x.ScopeId })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessPlatformMember>(b =>
        {
            b.ToTable("ACCESS_PlatformMembers");
            b.HasKey(x => x.UserId);
            b.Property(x => x.PlatformId).IsRequired();
            b.Property(x => x.MembershipGeneration).IsRequired();
            b.Property(x => x.JoinedAt).IsRequired();
            b.HasIndex(x => new { x.PlatformId, x.UserId });
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

        modelBuilder.Entity<AccessMembershipProjection>(b =>
        {
            b.ToTable("ACCESS_MembershipProjections");
            b.HasKey(x => new { x.TenantId, x.UserId });
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.MembershipGeneration).IsRequired();
            b.Property(x => x.LastAppliedSequence).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();

            // The primary key already enforces uniqueness; this index matches the committed migration.
            b.HasIndex(x => new { x.TenantId, x.UserId })
                .IsUnique()
                .HasDatabaseName("UX_ACCESS_MembershipProjections_Tenant_User");

            b.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_ACCESS_MembershipProjections_UserId");
        });

        modelBuilder.Entity<AccessMembershipStreamCursor>(b =>
        {
            b.ToTable("ACCESS_MembershipStreamCursors");
            b.HasKey(x => x.TenantId);
            b.Property(x => x.TenantId).ValueGeneratedNever();

            // Concurrency token: two consumers advancing the same tenant cannot both win.
            b.Property(x => x.LastAppliedSequence).IsRequired().IsConcurrencyToken();
            b.Property(x => x.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<AccessProcessedEvent>(b =>
        {
            b.ToTable("ACCESS_ProcessedEvents");
            b.HasKey(x => x.EventId);
            b.Property(x => x.EventId).ValueGeneratedNever();
            b.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            b.Property(x => x.ProcessedAt).IsRequired();

            // A tenant sequence is applied at most once, even if two different event ids claim it.
            b.HasIndex(x => new { x.TenantId, x.Sequence })
                .IsUnique()
                .HasDatabaseName("UX_ACCESS_ProcessedEvents_Tenant_Sequence");
        });

        modelBuilder.Entity<AccessMembershipWatermark>(b =>
        {
            b.ToTable("ACCESS_MembershipWatermarks");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            // Concurrency token: the watermark can never be moved backwards by a racing, older heartbeat.
            b.Property(x => x.AsOf).IsRequired().IsConcurrencyToken();
            b.Property(x => x.ReceivedAt).IsRequired();
        });

        modelBuilder.Entity<AccessQuarantinedTenant>(b =>
        {
            b.ToTable("ACCESS_QuarantinedTenants");
            b.HasKey(x => x.TenantId);
            b.Property(x => x.TenantId).ValueGeneratedNever();
            b.Property(x => x.Since).IsRequired();
        });

        modelBuilder.Entity<AccessTenantState>(b =>
        {
            b.ToTable("ACCESS_TenantStates");
            b.HasKey(x => x.TenantId);
            b.Property(x => x.TenantId).ValueGeneratedNever();
            b.Property(x => x.Status).IsRequired().HasMaxLength(20);
            b.Property(x => x.UpdatedAt).IsRequired();
        });
    }
}