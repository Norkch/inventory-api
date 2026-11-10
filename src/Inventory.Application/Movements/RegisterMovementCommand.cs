namespace Inventory.Application.Movements;

public sealed record RegisterMovementCommand(
    Guid ProductId,
    string? Type,
    int? Quantity,
    string? Reason,
    MovementRegistrant Registrant);
