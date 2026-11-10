namespace Inventory.Application.Products;

public sealed record UpdateProductCommand(Guid Id, string? Name, string? Description, Guid? CategoryId);
