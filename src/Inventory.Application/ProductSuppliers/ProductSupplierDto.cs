namespace Inventory.Application.ProductSuppliers;

public sealed record ProductSupplierDto(
    Guid ProductId,
    string ProductName,
    Guid SupplierId,
    string SupplierName,
    string? SupplierSku,
    DateTime CreatedAtUtc);
