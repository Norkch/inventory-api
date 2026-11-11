using System.Linq.Expressions;
using Inventory.Application.Common;
using Inventory.Application.ProductSuppliers;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence.Read;

public sealed class ProductSupplierReadRepository : IProductSupplierReadRepository
{
    private static readonly Expression<Func<ProductSupplierRow, ProductSupplierDto>> ToProductSupplierDto =
        row => new ProductSupplierDto(row.ProductId, row.ProductName, row.SupplierId, row.SupplierName, row.SupplierSku, row.CreatedAtUtc);

    private readonly InventoryReadDbContext _context;

    public ProductSupplierReadRepository(InventoryReadDbContext context)
    {
        _context = context;
    }

    public async Task<ProductSupplierDto?> FindVisibleAsync(ProductSupplierPair pair, CancellationToken cancellationToken)
    {
        return await QueryVisibleLinks()
            .Where(row => row.ProductId == pair.ProductId && row.SupplierId == pair.SupplierId)
            .Select(ToProductSupplierDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsActiveProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        return _context.Products.AsNoTracking().AnyAsync(product => product.Id == productId && product.IsActive, cancellationToken);
    }

    public Task<bool> ExistsActiveSupplierAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        return _context.Suppliers.AsNoTracking().AnyAsync(supplier => supplier.Id == supplierId && supplier.IsActive, cancellationToken);
    }

    public async Task<PagedResult<ProductSupplierDto>> ListByProductAsync(ProductSuppliersCriteria criteria, CancellationToken cancellationToken)
    {
        var productLinks = QueryVisibleLinks().Where(row => row.ProductId == criteria.ProductId);
        var totalCount = await productLinks.CountAsync(cancellationToken);
        var items = await productLinks
            .OrderBy(row => row.SupplierName)
            .ThenBy(row => row.SupplierId)
            .Skip(CalculateOffset(criteria.PageRequest))
            .Take(criteria.PageRequest.PageSize)
            .Select(ToProductSupplierDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductSupplierDto>(items, criteria.PageRequest, totalCount);
    }

    public async Task<PagedResult<ProductSupplierDto>> ListBySupplierAsync(SupplierProductsCriteria criteria, CancellationToken cancellationToken)
    {
        var supplierLinks = QueryVisibleLinks().Where(row => row.SupplierId == criteria.SupplierId);
        var totalCount = await supplierLinks.CountAsync(cancellationToken);
        var items = await supplierLinks
            .OrderBy(row => row.ProductName)
            .ThenBy(row => row.ProductId)
            .Skip(CalculateOffset(criteria.PageRequest))
            .Take(criteria.PageRequest.PageSize)
            .Select(ToProductSupplierDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductSupplierDto>(items, criteria.PageRequest, totalCount);
    }

    private IQueryable<ProductSupplierRow> QueryVisibleLinks()
    {
        return from link in _context.ProductSuppliers.AsNoTracking()
               join product in _context.Products.AsNoTracking() on link.ProductId equals product.Id
               join supplier in _context.Suppliers.AsNoTracking() on link.SupplierId equals supplier.Id
               where product.IsActive && supplier.IsActive
               select new ProductSupplierRow
               {
                   ProductId = link.ProductId,
                   ProductName = product.Name,
                   SupplierId = link.SupplierId,
                   SupplierName = supplier.Name,
                   SupplierSku = link.SupplierSku,
                   CreatedAtUtc = link.CreatedAtUtc
               };
    }

    private static int CalculateOffset(PageRequest pageRequest)
    {
        return (pageRequest.Page - 1) * pageRequest.PageSize;
    }
}
