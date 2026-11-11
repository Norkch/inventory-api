namespace Inventory.Application.ProductSuppliers;

public sealed record ProductSupplierLinkResult(ProductSupplierLinkStatus Status, ProductSupplierDto? Link)
{
    public static ProductSupplierLinkResult Linked(ProductSupplierDto link)
    {
        return new ProductSupplierLinkResult(ProductSupplierLinkStatus.Linked, link);
    }

    public static ProductSupplierLinkResult Rejected(ProductSupplierLinkStatus status)
    {
        return new ProductSupplierLinkResult(status, null);
    }
}
