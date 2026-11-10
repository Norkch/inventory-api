using Inventory.Domain.Entities;

namespace Inventory.Application.Movements;

public sealed record MovementRegistrationResult(MovementRegistrationStatus Status, InventoryMovement? Movement)
{
    public static MovementRegistrationResult Registered(InventoryMovement movement)
    {
        return new MovementRegistrationResult(MovementRegistrationStatus.Registered, movement);
    }

    public static MovementRegistrationResult Rejected(MovementRegistrationStatus status)
    {
        return new MovementRegistrationResult(status, null);
    }
}
