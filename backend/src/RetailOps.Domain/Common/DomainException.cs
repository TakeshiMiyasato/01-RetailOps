namespace RetailOps.Domain.Common;

/// <summary>A business rule was violated. Surfaces to clients as HTTP 422.</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class InsufficientStockException(string sku, int requested, int available)
    : DomainException("inventory.insufficient_stock", $"Insufficient stock for {sku}: requested {requested}, available {available}.")
{
    public string Sku { get; } = sku;
    public int Requested { get; } = requested;
    public int Available { get; } = available;
}
