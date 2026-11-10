using Inventory.Application.Common;

namespace Inventory.Application.Products;

public sealed class ListProductsHandler
{
    private readonly IProductReadRepository _productReadRepository;

    public ListProductsHandler(IProductReadRepository productReadRepository)
    {
        _productReadRepository = productReadRepository;
    }

    public Task<PagedResult<ProductDto>> HandleAsync(ListProductsQuery query, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Create(query.Page, query.PageSize);
        var criteria = new ProductListCriteria(pageRequest, query.CategoryId, ProductSearch.Normalize(query.Search));

        return _productReadRepository.ListActiveAsync(criteria, cancellationToken);
    }
}
