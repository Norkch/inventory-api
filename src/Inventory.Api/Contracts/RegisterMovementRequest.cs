using Inventory.Application.Movements;

namespace Inventory.Api.Contracts;

public sealed class RegisterMovementRequest
{
    public string? Type { get; set; }

    public int? Quantity { get; set; }

    public string? Reason { get; set; }

    public Guid? SupplierId { get; set; }

    public RegisterMovementCommand ToCommand(Guid productId, MovementRegistrant registrant)
    {
        return new RegisterMovementCommand(productId, Type, Quantity, Reason, SupplierId, registrant);
    }
}
