using Microsoft.EntityFrameworkCore;
using RetailOps.Application.Common;
using RetailOps.Domain.Common;
using RetailOps.Domain.Entities;
using RetailOps.Domain.Enums;

namespace RetailOps.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser) : DbContext(options), IAppDbContext
{
    /// <summary>
    /// Evaluated per query (EF parameterizes it), so a single model serves every tenant.
    /// Unauthenticated contexts (seeding, login) get Guid.Empty and see nothing unless they call IgnoreQueryFilters.
    /// </summary>
    private Guid CurrentTenantId => currentUser.IsAuthenticated ? currentUser.TenantId : Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums as readable strings: friendlier for ad-hoc SQL and for the analytics lakehouse that ingests this DB.
        builder.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<MovementType>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<TransferStatus>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<PaymentMethod>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        model.Entity<User>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        model.Entity<Store>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        model.Entity<Product>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        model.Entity<StockLevel>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        model.Entity<StockMovement>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        model.Entity<Sale>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        model.Entity<Transfer>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>New rows inherit the caller's tenant; an attempt to write into another tenant is rejected outright.</summary>
    private void StampTenant()
    {
        if (!currentUser.IsAuthenticated) return; // seeding sets TenantId explicitly

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                entry.Entity.TenantId = currentUser.TenantId;
            else if (entry.State is EntityState.Added or EntityState.Modified && entry.Entity.TenantId != currentUser.TenantId)
                throw new InvalidOperationException("Cross-tenant write blocked.");
        }
    }
}
