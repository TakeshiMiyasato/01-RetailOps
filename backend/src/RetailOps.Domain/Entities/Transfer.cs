using RetailOps.Domain.Common;
using RetailOps.Domain.Enums;

namespace RetailOps.Domain.Entities;

/// <summary>
/// Moves stock between two stores: Requested → InTransit (stock leaves origin) → Received (stock enters destination).
/// Cancelling is only allowed before shipping, so stock is never lost in transit.
/// </summary>
public class Transfer : TenantEntity
{
    private readonly List<TransferLine> _lines = [];

    public required string Number { get; set; }
    public Guid FromStoreId { get; set; }
    public Store? FromStore { get; set; }
    public Guid ToStoreId { get; set; }
    public Store? ToStore { get; set; }
    public TransferStatus Status { get; private set; } = TransferStatus.Requested;
    public Guid RequestedById { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ShippedAt { get; private set; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public string? Note { get; set; }
    public IReadOnlyList<TransferLine> Lines => _lines;

    public static Transfer Create(string number, Guid fromStoreId, Guid toStoreId, Guid requestedById, string? note = null)
    {
        if (fromStoreId == toStoreId)
            throw new DomainException("transfer.same_store", "Origin and destination stores must be different.");
        return new Transfer { Number = number, FromStoreId = fromStoreId, ToStoreId = toStoreId, RequestedById = requestedById, Note = note };
    }

    public void AddLine(Guid productId, string sku, int quantity)
    {
        EnsureStatus(TransferStatus.Requested, "add lines to");
        if (quantity <= 0)
            throw new DomainException("transfer.invalid_quantity", "Quantity must be positive.");
        var existing = _lines.FirstOrDefault(l => l.ProductId == productId);
        if (existing is not null)
            existing.Quantity += quantity;
        else
            _lines.Add(new TransferLine { ProductId = productId, Sku = sku, Quantity = quantity });
    }

    public void MarkShipped(DateTimeOffset at)
    {
        EnsureStatus(TransferStatus.Requested, "ship");
        if (_lines.Count == 0)
            throw new DomainException("transfer.empty", "Cannot ship a transfer without lines.");
        Status = TransferStatus.InTransit;
        ShippedAt = at;
    }

    public void MarkReceived(DateTimeOffset at)
    {
        EnsureStatus(TransferStatus.InTransit, "receive");
        Status = TransferStatus.Received;
        ReceivedAt = at;
    }

    public void Cancel()
    {
        EnsureStatus(TransferStatus.Requested, "cancel");
        Status = TransferStatus.Cancelled;
    }

    private void EnsureStatus(TransferStatus expected, string action)
    {
        if (Status != expected)
            throw new DomainException("transfer.invalid_state", $"Cannot {action} a transfer in status {Status}.");
    }
}

public class TransferLine : Entity
{
    public Guid TransferId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public required string Sku { get; set; }
    public int Quantity { get; set; }
}
