namespace Inventory.Domain.Entities;

public sealed record ProductSnapshot(
    Guid Id,
    Guid CategoryId,
    string Sku,
    string Name,
    string? Description,
    int StockQuantity,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
