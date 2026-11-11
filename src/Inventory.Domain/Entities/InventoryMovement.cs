namespace Inventory.Domain.Entities;

public sealed class InventoryMovement
{
    private InventoryMovement()
    {
    }

    public Guid Id { get; private init; }

    public Guid ProductId { get; private init; }

    public Guid? SupplierId { get; private init; }

    public MovementType Type { get; private init; }

    public int Quantity { get; private init; }

    public string? Reason { get; private init; }

    public string RegisteredBy { get; private init; } = string.Empty;

    public DateTime OccurredAtUtc { get; private init; }

    public int? StockAfter { get; private init; }

    public int StockDelta => Type == MovementType.Entry ? Quantity : -Quantity;

    public static InventoryMovement Create(MovementDraft draft)
    {
        var type = MovementRules.ValidateType(draft.Type);
        var quantity = MovementRules.ValidateQuantity(draft.Quantity);

        return new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ProductId = draft.ProductId,
            SupplierId = MovementRules.ValidateSupplier(type, draft.SupplierId),
            Type = type,
            Quantity = quantity,
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
            SupplierId = SupplierId,
            Type = Type,
            Quantity = Quantity,
            Reason = Reason,
            RegisteredBy = RegisteredBy,
            OccurredAtUtc = OccurredAtUtc,
            StockAfter = stockAfter
        };
    }
}
