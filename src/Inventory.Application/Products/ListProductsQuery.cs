namespace Inventory.Application.Products;

public sealed record ListProductsQuery(int? Page, int? PageSize, Guid? CategoryId, string? Search);
