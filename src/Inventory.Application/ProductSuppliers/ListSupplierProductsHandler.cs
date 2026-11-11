using Inventory.Application.Common;
using Inventory.Application.Suppliers;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.ProductSuppliers;

public sealed class ListSupplierProductsHandler
{
    private readonly IProductSupplierReadRepository _productSupplierReadRepository;

    public ListSupplierProductsHandler(IProductSupplierReadRepository productSupplierReadRepository)
    {
        _productSupplierReadRepository = productSupplierReadRepository;
    }

    public async Task<PagedResult<ProductSupplierDto>> HandleAsync(ListSupplierProductsQuery query, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Create(query.Page, query.PageSize);
        await EnsureSupplierIsActiveAsync(query, cancellationToken);

        return await _productSupplierReadRepository.ListBySupplierAsync(new SupplierProductsCriteria(query.SupplierId, pageRequest), cancellationToken);
    }

    private async Task EnsureSupplierIsActiveAsync(ListSupplierProductsQuery query, CancellationToken cancellationToken)
    {
        if (!await _productSupplierReadRepository.ExistsActiveSupplierAsync(query.SupplierId, cancellationToken))
        {
            throw new NotFoundException(SupplierMessages.ActiveSupplierNotFound(query.SupplierId));
        }
    }
}
