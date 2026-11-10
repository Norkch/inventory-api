using Inventory.Application.Abstractions;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.Products;

public sealed class UpdateProductHandler
{
    private readonly IProductWriteRepository _productWriteRepository;
    private readonly IClock _clock;

    public UpdateProductHandler(IProductWriteRepository productWriteRepository, IClock clock)
    {
        _productWriteRepository = productWriteRepository;
        _clock = clock;
    }

    public async Task<ProductDto> HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var changes = ProductChanges.Create(command.Id, command.Name, command.Description, command.CategoryId, _clock.UtcNow);
        await EnsureProductIsActiveAsync(changes, cancellationToken);
        var category = await FindActiveCategoryAsync(changes, cancellationToken);
        var product = await UpdateWhileCategoryIsActiveAsync(changes, cancellationToken);

        return ProductDto.From(product, category);
    }

    private async Task EnsureProductIsActiveAsync(ProductChanges changes, CancellationToken cancellationToken)
    {
        if (!await _productWriteRepository.ExistsActiveAsync(changes.ProductId, cancellationToken))
        {
            throw new NotFoundException(ProductMessages.ActiveProductNotFound(changes.ProductId));
        }
    }

    private async Task<CategoryReference> FindActiveCategoryAsync(ProductChanges changes, CancellationToken cancellationToken)
    {
        var category = await _productWriteRepository.FindActiveCategoryAsync(changes.CategoryId, cancellationToken);

        return category ?? throw new ValidationException(ProductMessages.CategoryNotActive);
    }

    private async Task<Product> UpdateWhileCategoryIsActiveAsync(ProductChanges changes, CancellationToken cancellationToken)
    {
        var product = await _productWriteRepository.UpdateAsync(changes, cancellationToken);
        if (product is null)
        {
            await EnsureProductIsActiveAsync(changes, cancellationToken);
            throw new ValidationException(ProductMessages.CategoryNotActive);
        }

        return product;
    }
}
