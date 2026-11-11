namespace Inventory.Domain.Entities;

public sealed record ProductSupplierDraft(
    Guid ProductId,
    Guid? SupplierId,
    string? SupplierSku,
    DateTime CreatedAtUtc);
