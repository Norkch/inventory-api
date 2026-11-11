using Inventory.Application.ProductSuppliers;

namespace Inventory.Api.Contracts;

public sealed class LinkProductSupplierRequest
{
    public Guid? SupplierId { get; set; }

    public string? SupplierSku { get; set; }

    public LinkProductSupplierCommand ToCommand(Guid productId)
    {
        return new LinkProductSupplierCommand(productId, SupplierId, SupplierSku);
    }
}
