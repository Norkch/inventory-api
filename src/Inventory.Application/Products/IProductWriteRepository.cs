using Inventory.Domain.Entities;

namespace Inventory.Application.Products;

public interface IProductWriteRepository
{
    Task<CategoryReference?> FindActiveCategoryAsync(Guid categoryId, CancellationToken cancellationToken);

    Task<bool> ExistsWithSameSkuAsync(Product product, CancellationToken cancellationToken);

    Task<bool> AddAsync(Product product, CancellationToken cancellationToken);

    Task<bool> ExistsActiveAsync(Guid productId, CancellationToken cancellationToken);

    Task<Product?> UpdateAsync(ProductChanges changes, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(ProductDeletion deletion, CancellationToken cancellationToken);
}
