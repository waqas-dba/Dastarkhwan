using System.Reflection;
using System.Reflection.Emit;

namespace CoreKit.Tenant.Isolation;

/// <summary>
/// Base class for any DbContext that holds tenant data. Entities that implement ITenantScoped get:
/// reads limited to the current tenant (and no rows at all when there is no tenant), TenantId filled in on
/// insert, and an exception on any write that touches another tenant's data.
/// Override <see cref="ConfigureModel"/> instead of OnModelCreating.
/// To work across tenants on purpose, use ICurrentTenant.Change(...) or IgnoreQueryFilters().
/// </summary>
public abstract class TenantAwareDbContext : DbContext
{
    public const string TenantFilterName = "CoreKit.Tenant.Isolation";

    private static readonly MethodInfo ApplyTenantFilterMethod = typeof(TenantAwareDbContext)
        .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ICurrentTenant _currentTenant;

    protected TenantAwareDbContext(DbContextOptions options, ICurrentTenant currentTenant) : base(options)
    {
        _currentTenant = currentTenant;
    }

    /// <summary>Read by the query filter on every query, so it follows the tenant of the moment.</summary>
    protected Guid? CurrentTenantId => _currentTenant.Id;

    protected virtual void ConfigureModel(ModelBuilder modelBuilder)
    {
    }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureModel(modelBuilder);

        var tenantScopedRoots = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType) && t.BaseType is null && !t.IsOwned())
            .ToList();

        foreach (var entityType in tenantScopedRoots)
            ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { modelBuilder });
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScoped
        => modelBuilder.Entity<TEntity>().HasQueryFilter(TenantFilterName, e => e.TenantId == CurrentTenantId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantIsolation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceTenantIsolation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforceTenantIsolation()
    {
        ChangeTracker.DetectChanges();

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    {
                        var tenantId = RequireTenantId();

                        if (entry.Entity.TenantId == Guid.Empty)
                            entry.Entity.TenantId = tenantId;
                        else if (entry.Entity.TenantId != tenantId)
                            throw new TenantIsolationException(
                                $"Cannot insert a {entry.Metadata.ClrType.Name} for another tenant.");

                        break;
                    }

                case EntityState.Modified:
                case EntityState.Deleted:
                    {
                        var tenantId = RequireTenantId();
                        var property = entry.Property(nameof(ITenantScoped.TenantId));
                        var original = (Guid)property.OriginalValue!;

                        if (original != tenantId)
                            throw new TenantIsolationException(
                                $"Cannot change or delete a {entry.Metadata.ClrType.Name} that belongs to another tenant.");

                        if (entry.State == EntityState.Modified && (Guid)property.CurrentValue! != original)
                            throw new TenantIsolationException(
                                $"Cannot move a {entry.Metadata.ClrType.Name} to another tenant.");

                        break;
                    }
            }
        }
    }

    private Guid RequireTenantId() => _currentTenant.Id ?? throw new TenantNotResolvedException();
}