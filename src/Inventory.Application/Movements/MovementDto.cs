using Inventory.Domain.Entities;

namespace Inventory.Application.Movements;

public sealed record MovementDto(
    Guid Id,
    Guid ProductId,
    string Type,
    int Quantity,
    int StockAfter,
    string? Reason,
    DateTime OccurredAtUtc,
    string RegisteredBy)
{
    public static MovementDto From(InventoryMovement movement)
    {
        return new MovementDto(
            movement.Id,
            movement.ProductId,
            movement.Type.ToString(),
            movement.Quantity,
            movement.StockAfter ?? throw new InvalidOperationException("The movement has not been registered yet."),
            movement.Reason,
            movement.OccurredAtUtc,
            movement.RegisteredBy);
    }
}
