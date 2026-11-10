using Inventory.Application.Common;
using Inventory.Application.Products;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.Movements;

public sealed class ListMovementsHandler
{
    private readonly IMovementReadRepository _movementReadRepository;

    public ListMovementsHandler(IMovementReadRepository movementReadRepository)
    {
        _movementReadRepository = movementReadRepository;
    }

    public async Task<PagedResult<MovementDto>> HandleAsync(ListMovementsQuery query, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Create(query.Page, query.PageSize);
        await EnsureProductIsActiveAsync(query, cancellationToken);

        return await _movementReadRepository.ListAsync(new MovementListCriteria(query.ProductId, pageRequest), cancellationToken);
    }

    private async Task EnsureProductIsActiveAsync(ListMovementsQuery query, CancellationToken cancellationToken)
    {
        if (!await _movementReadRepository.ExistsActiveProductAsync(query.ProductId, cancellationToken))
        {
            throw new NotFoundException(ProductMessages.ActiveProductNotFound(query.ProductId));
        }
    }
}
