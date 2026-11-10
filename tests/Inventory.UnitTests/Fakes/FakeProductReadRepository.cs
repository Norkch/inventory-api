using Inventory.Application.Common;
using Inventory.Application.Products;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeProductReadRepository : IProductReadRepository
{
    public List<ProductDto> ActiveProducts { get; } = new();

    public ProductListCriteria? ReceivedCriteria { get; private set; }

    public Task<ProductDto?> FindActiveByIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        return Task.FromResult(ActiveProducts.FirstOrDefault(product => product.Id == productId));
    }

    public Task<PagedResult<ProductDto>> ListActiveAsync(ProductListCriteria criteria, CancellationToken cancellationToken)
    {
        ReceivedCriteria = criteria;

        return Task.FromResult(new PagedResult<ProductDto>(ActiveProducts, criteria.PageRequest, ActiveProducts.Count));
    }
}
