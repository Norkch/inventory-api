namespace Inventory.Application.Movements;

public enum MovementRegistrationStatus
{
    Registered,
    ProductNotFound,
    SupplierNotAvailable,
    InsufficientStock,
    StockOverflow
}
