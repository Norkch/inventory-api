namespace Inventory.Infrastructure.Persistence.Read;

public sealed class ProductSupplierRow
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public string SupplierName { get; set; } = string.Empty;

    public string? SupplierSku { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
