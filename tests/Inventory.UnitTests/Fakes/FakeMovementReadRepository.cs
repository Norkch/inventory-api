using Inventory.Application.Common;
using Inventory.Application.Movements;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeMovementReadRepository : IMovementReadRepository
{
    public List<Guid> ActiveProductIds { get; } = new();

    public List<MovementDto> Movements { get; } = new();

    public List<string> Calls { get; } = new();

    public MovementListCriteria? ReceivedCriteria { get; private set; }

    public Task<bool> ExistsActiveProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ExistsActiveProductAsync));

        return Task.FromResult(ActiveProductIds.Contains(productId));
    }

    public Task<PagedResult<MovementDto>> ListAsync(MovementListCriteria criteria, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ListAsync));
        ReceivedCriteria = criteria;
        var items = Movements.Where(movement => movement.ProductId == criteria.ProductId).ToList();

        return Task.FromResult(new PagedResult<MovementDto>(items, criteria.PageRequest, items.Count));
    }
}
