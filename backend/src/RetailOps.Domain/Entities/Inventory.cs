using RetailOps.Domain.Common;
using RetailOps.Domain.Enums;

namespace RetailOps.Domain.Entities;

/// <summary>
/// On-hand quantity of one product in one store. Every change goes through
/// <see cref="Increase"/> / <see cref="Decrease"/> so stock can never go negative,
/// and <see cref="RowVersion"/> gives optimistic concurrency between concurrent sales.
/// </summary>
public class StockLevel : TenantEntity
{
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; private set; }
    public int ReorderPoint { get; set; } = 10;
    public byte[] RowVersion { get; set; } = [];

    public bool IsLow => Quantity <= ReorderPoint;

    public static StockLevel Create(Guid storeId, Guid productId, int initialQuantity = 0, int reorderPoint = 10)
    {
        if (initialQuantity < 0)
            throw new DomainException("inventory.invalid_quantity", "Initial quantity cannot be negative.");
        return new StockLevel { StoreId = storeId, ProductId = productId, Quantity = initialQuantity, ReorderPoint = reorderPoint };
    }

    public void Increase(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("inventory.invalid_quantity", "Quantity must be positive.");
        Quantity += quantity;
    }

    public void Decrease(int quantity, string sku)
    {
        if (quantity <= 0)
            throw new DomainException("inventory.invalid_quantity", "Quantity must be positive.");
        if (quantity > Quantity)
            throw new InsufficientStockException(sku, quantity, Quantity);
        Quantity -= quantity;
    }
}

/// <summary>Append-only ledger of every stock change. Quantity is signed (negative = outflow).</summary>
public class StockMovement : TenantEntity
{
    public Guid StoreId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public MovementType Type { get; set; }
    public required string Reference { get; set; }
    public string? Note { get; set; }
    public Guid? UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
