namespace Inventory.Infrastructure.Persistence.Read;

public sealed class ProductSupplierRecord
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Guid SupplierId { get; set; }

    public string? SupplierSku { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
