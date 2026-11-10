using Inventory.Application.Common;

namespace Inventory.Application.Products;

public interface IProductReadRepository
{
    Task<ProductDto?> FindActiveByIdAsync(Guid productId, CancellationToken cancellationToken);

    Task<PagedResult<ProductDto>> ListActiveAsync(ProductListCriteria criteria, CancellationToken cancellationToken);
}
