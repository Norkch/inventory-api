namespace Inventory.Domain.Entities;

public sealed record SupplierEdit(
    Guid SupplierId,
    string? Name,
    string? Email,
    string? Phone,
    DateTime UpdatedAtUtc);
