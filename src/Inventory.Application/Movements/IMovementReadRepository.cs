using Inventory.Application.Common;

namespace Inventory.Application.Movements;

public interface IMovementReadRepository
{
    Task<bool> ExistsActiveProductAsync(Guid productId, CancellationToken cancellationToken);

    Task<PagedResult<MovementDto>> ListAsync(MovementListCriteria criteria, CancellationToken cancellationToken);
}
