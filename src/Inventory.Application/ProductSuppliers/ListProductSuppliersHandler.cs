using Inventory.Application.Common;
using Inventory.Application.Products;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.ProductSuppliers;

public sealed class ListProductSuppliersHandler
{
    private readonly IProductSupplierReadRepository _productSupplierReadRepository;

    public ListProductSuppliersHandler(IProductSupplierReadRepository productSupplierReadRepository)
    {
        _productSupplierReadRepository = productSupplierReadRepository;
    }

    public async Task<PagedResult<ProductSupplierDto>> HandleAsync(ListProductSuppliersQuery query, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Create(query.Page, query.PageSize);
        await EnsureProductIsActiveAsync(query, cancellationToken);

        return await _productSupplierReadRepository.ListByProductAsync(new ProductSuppliersCriteria(query.ProductId, pageRequest), cancellationToken);
    }

    private async Task EnsureProductIsActiveAsync(ListProductSuppliersQuery query, CancellationToken cancellationToken)
    {
        if (!await _productSupplierReadRepository.ExistsActiveProductAsync(query.ProductId, cancellationToken))
        {
            throw new NotFoundException(ProductMessages.ActiveProductNotFound(query.ProductId));
        }
    }
}
