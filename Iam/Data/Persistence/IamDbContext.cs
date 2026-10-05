using Iam.Core.Abstractions;
using Iam.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iam.Data.Persistence;

public sealed class IamDbContext : DbContext, IUnitOfWork
{
    /// <summary>All IAM tables live in their own schema, so they never mix with another app's tables.</summary>
    public const string Schema = "iam";

    public IamDbContext(DbContextOptions<IamDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ClientApplication> ClientApplications => Set<ClientApplication>();
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IamDbContext).Assembly);
    }
}