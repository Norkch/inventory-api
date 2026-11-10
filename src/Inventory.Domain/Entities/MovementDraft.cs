namespace Inventory.Domain.Entities;

public sealed record MovementDraft(
    Guid ProductId,
    string? Type,
    int? Quantity,
    string? Reason,
    string? RegisteredBy,
    DateTime OccurredAtUtc);
