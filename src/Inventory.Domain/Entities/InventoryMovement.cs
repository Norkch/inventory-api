namespace Inventory.Domain.Entities;

public sealed class InventoryMovement
{
    private InventoryMovement()
    {
    }

    public Guid Id { get; private init; }

    public Guid ProductId { get; private init; }

    public MovementType Type { get; private init; }

    public int Quantity { get; private init; }

    public string? Reason { get; private init; }

    public string RegisteredBy { get; private init; } = string.Empty;

    public DateTime OccurredAtUtc { get; private init; }

    public int? StockAfter { get; private init; }

    public int StockDelta => Type == MovementType.Entry ? Quantity : -Quantity;

    public static InventoryMovement Create(MovementDraft draft)
    {
        return new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = draft.ProductId,
            Type = MovementRules.ValidateType(draft.Type),
            Quantity = MovementRules.ValidateQuantity(draft.Quantity),
            Reason = MovementRules.ValidateReason(draft.Reason),
            RegisteredBy = MovementRules.ValidateRegisteredBy(draft.RegisteredBy),
            OccurredAtUtc = draft.OccurredAtUtc
        };
    }

    public InventoryMovement WithStockAfter(int stockAfter)
    {
        return new InventoryMovement
        {
            Id = Id,
            ProductId = ProductId,
            Type = Type,
            Quantity = Quantity,
            Reason = Reason,
            RegisteredBy = RegisteredBy,
            OccurredAtUtc = OccurredAtUtc,
            StockAfter = stockAfter
        };
    }
}
