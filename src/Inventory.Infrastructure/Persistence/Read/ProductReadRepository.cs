using System.Linq.Expressions;
using Inventory.Application.Common;
using Inventory.Application.Products;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence.Read;

public sealed class ProductReadRepository : IProductReadRepository
{
    private const string CaseInsensitiveCollation = "SQL_Latin1_General_CP1_CI_AS";

    private static readonly Expression<Func<ProductWithCategory, ProductDto>> ToProductDto =
        row => new ProductDto(
            row.Product.Id,
            row.Product.Sku,
            row.Product.Name,
            row.Product.Description,
            row.Product.CategoryId,
            row.CategoryName,
            row.Product.StockQuantity,
            row.Product.CreatedAtUtc,
            row.Product.UpdatedAtUtc);

    private readonly InventoryReadDbContext _context;

    public ProductReadRepository(InventoryReadDbContext context)
    {
        _context = context;
    }

    public async Task<ProductDto?> FindActiveByIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await QueryActiveProductsWithCategory()
            .Where(row => row.Product.Id == productId)
            .Select(ToProductDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<ProductDto>> ListActiveAsync(ProductListCriteria criteria, CancellationToken cancellationToken)
    {
        var matchingProducts = FilterBySearch(FilterByCategory(QueryActiveProductsWithCategory(), criteria), criteria);
        var totalCount = await matchingProducts.CountAsync(cancellationToken);
        var items = await matchingProducts
            .OrderBy(row => row.Product.Name)
            .ThenBy(row => row.Product.Id)
            .Skip(CalculateOffset(criteria.PageRequest))
            .Take(criteria.PageRequest.PageSize)
            .Select(ToProductDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items, criteria.PageRequest, totalCount);
    }

    private IQueryable<ProductWithCategory> QueryActiveProductsWithCategory()
    {
        return _context.Products.AsNoTracking()
            .Where(product => product.IsActive)
            .Join(
                _context.Categories.AsNoTracking(),
                product => product.CategoryId,
                category => category.Id,
                (product, category) => new ProductWithCategory { Product = product, CategoryName = category.Name });
    }

    private static IQueryable<ProductWithCategory> FilterByCategory(IQueryable<ProductWithCategory> products, ProductListCriteria criteria)
    {
        if (criteria.CategoryId is null)
        {
            return products;
        }
        var categoryId = criteria.CategoryId.Value;

        return products.Where(row => row.Product.CategoryId == categoryId);
    }

    private static IQueryable<ProductWithCategory> FilterBySearch(IQueryable<ProductWithCategory> products, ProductListCriteria criteria)
    {
        if (criteria.Search is null)
        {
            return products;
        }
        var pattern = LikePattern.ForContains(criteria.Search);

        return products.Where(row =>
            EF.Functions.Like(EF.Functions.Collate(row.Product.Name, CaseInsensitiveCollation), pattern, LikePattern.EscapeCharacter)
            || EF.Functions.Like(row.Product.Sku, pattern, LikePattern.EscapeCharacter));
    }

    private static int CalculateOffset(PageRequest pageRequest)
    {
        return (pageRequest.Page - 1) * pageRequest.PageSize;
    }
}
