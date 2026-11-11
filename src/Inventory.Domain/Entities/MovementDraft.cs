namespace Inventory.Domain.Entities;

public sealed record MovementDraft(
    Guid ProductId,
    string? Type,
    int? Quantity,
    string? Reason,
    Guid? SupplierId,
    string? RegisteredBy,
    DateTime OccurredAtUtc);
