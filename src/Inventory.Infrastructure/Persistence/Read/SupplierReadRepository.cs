using System.Linq.Expressions;
using Inventory.Application.Common;
using Inventory.Application.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence.Read;

public sealed class SupplierReadRepository : ISupplierReadRepository
{
    private static readonly Expression<Func<SupplierRecord, SupplierDto>> ToSupplierDto =
        record => new SupplierDto(record.Id, record.Name, record.Nit, record.Email, record.Phone, record.CreatedAtUtc, record.UpdatedAtUtc);

    private readonly InventoryReadDbContext _context;

    public SupplierReadRepository(InventoryReadDbContext context)
    {
        _context = context;
    }

    public async Task<SupplierDto?> FindActiveAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        return await QueryActiveSuppliers()
            .Where(record => record.Id == supplierId)
            .Select(ToSupplierDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<SupplierDto>> ListAsync(SupplierListCriteria criteria, CancellationToken cancellationToken)
    {
        var activeSuppliers = QueryActiveSuppliers();
        var totalCount = await activeSuppliers.CountAsync(cancellationToken);
        var items = await activeSuppliers
            .OrderBy(record => record.Name)
            .ThenBy(record => record.Id)
            .Skip(CalculateOffset(criteria.PageRequest))
            .Take(criteria.PageRequest.PageSize)
            .Select(ToSupplierDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierDto>(items, criteria.PageRequest, totalCount);
    }

    private IQueryable<SupplierRecord> QueryActiveSuppliers()
    {
        return _context.Suppliers.AsNoTracking().Where(record => record.IsActive);
    }

    private static int CalculateOffset(PageRequest pageRequest)
    {
        return (pageRequest.Page - 1) * pageRequest.PageSize;
    }
}
