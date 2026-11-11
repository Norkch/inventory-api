using Inventory.Application.Common;

namespace Inventory.Application.ProductSuppliers;

public interface IProductSupplierReadRepository
{
    Task<ProductSupplierDto?> FindVisibleAsync(ProductSupplierPair pair, CancellationToken cancellationToken);

    Task<bool> ExistsActiveProductAsync(Guid productId, CancellationToken cancellationToken);

    Task<bool> ExistsActiveSupplierAsync(Guid supplierId, CancellationToken cancellationToken);

    Task<PagedResult<ProductSupplierDto>> ListByProductAsync(ProductSuppliersCriteria criteria, CancellationToken cancellationToken);

    Task<PagedResult<ProductSupplierDto>> ListBySupplierAsync(SupplierProductsCriteria criteria, CancellationToken cancellationToken);
}
