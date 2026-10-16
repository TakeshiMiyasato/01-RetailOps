namespace RetailOps.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Manager = 2,
    Cashier = 3
}

public enum MovementType
{
    Sale = 1,
    TransferOut = 2,
    TransferIn = 3,
    Adjustment = 4,
    Receipt = 5
}

public enum TransferStatus
{
    Requested = 1,
    InTransit = 2,
    Received = 3,
    Cancelled = 4
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    QrTransfer = 3
}
