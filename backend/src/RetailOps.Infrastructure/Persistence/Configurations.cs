using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetailOps.Domain.Entities;

namespace RetailOps.Infrastructure.Persistence;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Slug).HasMaxLength(60);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.Email).HasMaxLength(160);
        b.Property(x => x.FullName).HasMaxLength(120);
        b.Property(x => x.PasswordHash).HasMaxLength(500);
        // Email is the login key before the tenant is known, so it must be unique across tenants.
        b.HasIndex(x => x.Email).IsUnique();
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> b)
    {
        b.Property(x => x.Code).HasMaxLength(16);
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.City).HasMaxLength(80);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Sku).HasMaxLength(32);
        b.Property(x => x.Barcode).HasMaxLength(14);
        b.Property(x => x.Name).HasMaxLength(160);
        b.Property(x => x.Category).HasMaxLength(60);
        b.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Barcode }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Category });
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> b)
    {
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.StoreId, x.ProductId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.StoreId });
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.IsLow);
        b.ToTable(t => t.HasCheckConstraint("CK_StockLevels_Quantity_NonNegative", "[Quantity] >= 0"));
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.Property(x => x.Reference).HasMaxLength(60);
        b.Property(x => x.Note).HasMaxLength(300);
        b.HasIndex(x => new { x.TenantId, x.StoreId, x.CreatedAt });
        b.HasIndex(x => new { x.TenantId, x.Reference });
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.Property(x => x.Number).HasMaxLength(40);
        b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.StoreId, x.CreatedAt });
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> b)
    {
        b.Property(x => x.ProductName).HasMaxLength(160);
        b.Property(x => x.Sku).HasMaxLength(32);
        b.Ignore(x => x.LineTotal);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> b)
    {
        b.Property(x => x.Number).HasMaxLength(40);
        b.Property(x => x.Note).HasMaxLength(300);
        b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Status });
        b.HasOne(x => x.FromStore).WithMany().HasForeignKey(x => x.FromStoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ToStore).WithMany().HasForeignKey(x => x.ToStoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.TransferId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TransferLineConfiguration : IEntityTypeConfiguration<TransferLine>
{
    public void Configure(EntityTypeBuilder<TransferLine> b)
    {
        b.Property(x => x.Sku).HasMaxLength(32);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
