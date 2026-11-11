namespace Inventory.Domain.Entities;

public sealed class ProductSupplier
{
    private ProductSupplier(ProductSupplierDraft validDraft)
    {
        Id = Guid.NewGuid();
        ProductId = validDraft.ProductId;
        SupplierId = validDraft.SupplierId!.Value;
        SupplierSku = validDraft.SupplierSku;
        CreatedAtUtc = validDraft.CreatedAtUtc;
    }

    public Guid Id { get; }

    public Guid ProductId { get; }

    public Guid SupplierId { get; }

    public string? SupplierSku { get; }

    public DateTime CreatedAtUtc { get; }

    public static ProductSupplier Create(ProductSupplierDraft draft)
    {
        var validDraft = draft with
        {
            SupplierId = ProductSupplierRules.ValidateSupplierId(draft.SupplierId),
            SupplierSku = ProductSupplierRules.ValidateSupplierSku(draft.SupplierSku)
        };

        return new ProductSupplier(validDraft);
    }
}
