using Inventory.Application.Movements;
using Inventory.Domain.Entities;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeMovementWriteRepository : IMovementWriteRepository
{
    public MovementRegistrationStatus Status { get; set; } = MovementRegistrationStatus.Registered;

    public int StockAfter { get; set; }

    public List<InventoryMovement> Received { get; } = new();

    public Task<MovementRegistrationResult> RegisterAsync(InventoryMovement movement, CancellationToken cancellationToken)
    {
        Received.Add(movement);
        var result = Status == MovementRegistrationStatus.Registered
            ? MovementRegistrationResult.Registered(movement.WithStockAfter(StockAfter))
            : MovementRegistrationResult.Rejected(Status);

        return Task.FromResult(result);
    }
}
