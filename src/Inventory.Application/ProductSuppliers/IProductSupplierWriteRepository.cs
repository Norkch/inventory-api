using Inventory.Domain.Entities;

namespace Inventory.Application.ProductSuppliers;

public interface IProductSupplierWriteRepository
{
    Task<ProductSupplierLinkResult> LinkAsync(ProductSupplier link, CancellationToken cancellationToken);
}
