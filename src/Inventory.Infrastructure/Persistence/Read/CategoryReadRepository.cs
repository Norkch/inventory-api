using System.Linq.Expressions;
using Inventory.Application.Categories;
using Inventory.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence.Read;

public sealed class CategoryReadRepository : ICategoryReadRepository
{
    private static readonly Expression<Func<CategoryRecord, CategoryDto>> ToCategoryDto =
        record => new CategoryDto(record.Id, record.Name, record.Description, record.CreatedAtUtc);

    private readonly InventoryReadDbContext _context;

    public CategoryReadRepository(InventoryReadDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryDto?> FindActiveByIdAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        return await QueryActiveCategories()
            .Where(record => record.Id == categoryId)
            .Select(ToCategoryDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<CategoryDto>> ListActiveAsync(PageRequest pageRequest, CancellationToken cancellationToken)
    {
        var activeCategories = QueryActiveCategories();
        var totalCount = await activeCategories.CountAsync(cancellationToken);
        var items = await activeCategories
            .OrderBy(record => record.Name)
            .ThenBy(record => record.Id)
            .Skip(CalculateOffset(pageRequest))
            .Take(pageRequest.PageSize)
            .Select(ToCategoryDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<CategoryDto>(items, pageRequest, totalCount);
    }

    private IQueryable<CategoryRecord> QueryActiveCategories()
    {
        return _context.Categories.AsNoTracking().Where(record => record.IsActive);
    }

    private static int CalculateOffset(PageRequest pageRequest)
    {
        return (pageRequest.Page - 1) * pageRequest.PageSize;
    }
}
