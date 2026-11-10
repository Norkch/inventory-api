using Inventory.Domain.Entities;

namespace Inventory.Application.Products;

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    int StockQuantity,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc)
{
    public static ProductDto From(Product product, CategoryReference category)
    {
        return new ProductDto(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.CategoryId,
            category.Name,
            product.StockQuantity,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);
    }
}
