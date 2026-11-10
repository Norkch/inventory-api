using Inventory.Application.Products;
using Inventory.Domain.Entities;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeProductWriteRepository : IProductWriteRepository
{
    public List<CategoryReference> ActiveCategories { get; } = new();

    public List<string> ExistingSkus { get; } = new();

    public List<Guid> ActiveProductIds { get; } = new();

    public List<Product> Added { get; } = new();

    public List<string> Calls { get; } = new();

    public bool AddSucceeds { get; set; } = true;

    public Product? UpdateResult { get; set; }

    public bool DeactivatesProductOnUpdate { get; set; }

    public ProductChanges? AppliedChanges { get; private set; }

    public ProductDeletion? AppliedDeletion { get; private set; }

    public Task<CategoryReference?> FindActiveCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(FindActiveCategoryAsync));

        return Task.FromResult(ActiveCategories.FirstOrDefault(category => category.Id == categoryId));
    }

    public Task<bool> ExistsWithSameSkuAsync(Product product, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ExistsWithSameSkuAsync));

        return Task.FromResult(ExistingSkus.Contains(product.Sku, StringComparer.OrdinalIgnoreCase));
    }

    public Task<bool> AddAsync(Product product, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(AddAsync));
        if (AddSucceeds)
        {
            Added.Add(product);
        }

        return Task.FromResult(AddSucceeds);
    }

    public Task<bool> ExistsActiveAsync(Guid productId, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ExistsActiveAsync));

        return Task.FromResult(ActiveProductIds.Contains(productId));
    }

    public Task<Product?> UpdateAsync(ProductChanges changes, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(UpdateAsync));
        AppliedChanges = changes;
        if (DeactivatesProductOnUpdate)
        {
            ActiveProductIds.Remove(changes.ProductId);
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<bool> DeleteAsync(ProductDeletion deletion, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(DeleteAsync));
        AppliedDeletion = deletion;

        return Task.FromResult(ActiveProductIds.Remove(deletion.ProductId));
    }
}
