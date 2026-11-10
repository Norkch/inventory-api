using Inventory.Application.Abstractions;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.Products;

public sealed class DeleteProductHandler
{
    private readonly IProductWriteRepository _productWriteRepository;
    private readonly IClock _clock;

    public DeleteProductHandler(IProductWriteRepository productWriteRepository, IClock clock)
    {
        _productWriteRepository = productWriteRepository;
        _clock = clock;
    }

    public async Task HandleAsync(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var deletion = new ProductDeletion(command.Id, _clock.UtcNow);
        if (!await _productWriteRepository.DeleteAsync(deletion, cancellationToken))
        {
            throw new NotFoundException(ProductMessages.ActiveProductNotFound(command.Id));
        }
    }
}
