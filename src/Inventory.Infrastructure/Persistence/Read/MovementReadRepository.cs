using Inventory.Application.Common;
using Inventory.Application.Movements;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence.Read;

public sealed class MovementReadRepository : IMovementReadRepository
{
    private readonly InventoryReadDbContext _context;

    public MovementReadRepository(InventoryReadDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsActiveProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        return _context.Products.AsNoTracking().AnyAsync(product => product.Id == productId && product.IsActive, cancellationToken);
    }

    public async Task<PagedResult<MovementDto>> ListAsync(MovementListCriteria criteria, CancellationToken cancellationToken)
    {
        var productMovements = _context.Movements.AsNoTracking().Where(movement => movement.ProductId == criteria.ProductId);
        var totalCount = await productMovements.CountAsync(cancellationToken);
        var records = await productMovements
            .OrderByDescending(movement => movement.OccurredAtUtc)
            .ThenByDescending(movement => movement.Id)
            .Skip(CalculateOffset(criteria.PageRequest))
            .Take(criteria.PageRequest.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MovementDto>(records.Select(ToMovementDto).ToList(), criteria.PageRequest, totalCount);
    }

    private static MovementDto ToMovementDto(MovementRecord record)
    {
        return new MovementDto(
            record.Id,
            record.ProductId,
            ((MovementType)record.MovementType).ToString(),
            record.Quantity,
            record.StockAfter,
            record.Reason,
            record.OccurredAtUtc,
            record.RegisteredBy);
    }

    private static int CalculateOffset(PageRequest pageRequest)
    {
        return (pageRequest.Page - 1) * pageRequest.PageSize;
    }
}
