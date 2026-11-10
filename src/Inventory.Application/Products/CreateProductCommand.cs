namespace Inventory.Application.Products;

public sealed record CreateProductCommand(string? Sku, string? Name, string? Description, Guid? CategoryId);
