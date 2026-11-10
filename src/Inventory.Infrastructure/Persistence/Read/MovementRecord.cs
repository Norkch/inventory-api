namespace Inventory.Infrastructure.Persistence.Read;

public sealed class MovementRecord
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Guid? SupplierId { get; set; }

    public byte MovementType { get; set; }

    public int Quantity { get; set; }

    public int StockAfter { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string RegisteredBy { get; set; } = string.Empty;
}
