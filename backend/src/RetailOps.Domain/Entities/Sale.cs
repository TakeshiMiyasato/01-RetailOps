using RetailOps.Domain.Common;
using RetailOps.Domain.Enums;

namespace RetailOps.Domain.Entities;

public class Sale : TenantEntity
{
    private readonly List<SaleLine> _lines = [];

    public required string Number { get; set; }
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }
    public Guid CashierId { get; set; }
    public User? Cashier { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Total { get; private set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<SaleLine> Lines => _lines;

    public void AddLine(Product product, int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("sale.invalid_quantity", "Quantity must be positive.");
        if (!product.IsActive)
            throw new DomainException("sale.inactive_product", $"Product {product.Sku} is not active.");

        var existing = _lines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existing is not null)
            existing.Quantity += quantity;
        else
            _lines.Add(new SaleLine
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                UnitPrice = product.Price,
                Quantity = quantity
            });

        Total = _lines.Sum(l => l.LineTotal);
    }

    public void EnsureNotEmpty()
    {
        if (_lines.Count == 0)
            throw new DomainException("sale.empty", "A sale must contain at least one line.");
    }
}

public class SaleLine : Entity
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}
