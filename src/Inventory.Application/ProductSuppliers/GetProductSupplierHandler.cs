using Inventory.Domain.Exceptions;

namespace Inventory.Application.ProductSuppliers;

public sealed class GetProductSupplierHandler
{
    private readonly IProductSupplierReadRepository _productSupplierReadRepository;

    public GetProductSupplierHandler(IProductSupplierReadRepository productSupplierReadRepository)
    {
        _productSupplierReadRepository = productSupplierReadRepository;
    }

    public async Task<ProductSupplierDto> HandleAsync(GetProductSupplierQuery query, CancellationToken cancellationToken)
    {
        var pair = new ProductSupplierPair(query.ProductId, query.SupplierId);
        var link = await _productSupplierReadRepository.FindVisibleAsync(pair, cancellationToken);

        return link ?? throw new NotFoundException(ProductSupplierMessages.NotLinked);
    }
}
