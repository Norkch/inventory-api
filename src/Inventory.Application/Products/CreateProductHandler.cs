using Inventory.Application.Abstractions;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.Products;

public sealed class CreateProductHandler
{
    private readonly IProductWriteRepository _productWriteRepository;
    private readonly IClock _clock;

    public CreateProductHandler(IProductWriteRepository productWriteRepository, IClock clock)
    {
        _productWriteRepository = productWriteRepository;
        _clock = clock;
    }

    public async Task<ProductDto> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = Product.Create(command.Sku, command.Name, command.Description, command.CategoryId, _clock.UtcNow);
        var category = await FindActiveCategoryAsync(product, cancellationToken);
        await EnsureSkuIsFreeAsync(product, cancellationToken);
        await AddWhileCategoryIsActiveAsync(product, cancellationToken);

        return ProductDto.From(product, category);
    }

    private async Task<CategoryReference> FindActiveCategoryAsync(Product product, CancellationToken cancellationToken)
    {
        var category = await _productWriteRepository.FindActiveCategoryAsync(product.CategoryId, cancellationToken);

        return category ?? throw new ValidationException(ProductMessages.CategoryNotActive);
    }

    private async Task EnsureSkuIsFreeAsync(Product product, CancellationToken cancellationToken)
    {
        if (await _productWriteRepository.ExistsWithSameSkuAsync(product, cancellationToken))
        {
            throw new ConflictException(ProductMessages.SkuAlreadyExists(product));
        }
    }

    private async Task AddWhileCategoryIsActiveAsync(Product product, CancellationToken cancellationToken)
    {
        if (!await _productWriteRepository.AddAsync(product, cancellationToken))
        {
            throw new ValidationException(ProductMessages.CategoryNotActive);
        }
    }
}
