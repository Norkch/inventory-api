using Inventory.Domain.Entities;

namespace Inventory.Application.Movements;

public interface IMovementWriteRepository
{
    Task<MovementRegistrationResult> RegisterAsync(InventoryMovement movement, CancellationToken cancellationToken);
}
