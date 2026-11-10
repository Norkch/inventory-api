using Inventory.Domain.Exceptions;

namespace Inventory.Application.Products;

public sealed class GetProductByIdHandler
{
    private readonly IProductReadRepository _productReadRepository;

    public GetProductByIdHandler(IProductReadRepository productReadRepository)
    {
        _productReadRepository = productReadRepository;
    }

    public async Task<ProductDto> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var product = await _productReadRepository.FindActiveByIdAsync(query.Id, cancellationToken);

        return product ?? throw new NotFoundException(ProductMessages.ActiveProductNotFound(query.Id));
    }
}
