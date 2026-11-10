namespace Inventory.Application.Products;

public sealed record ProductDeletion(Guid ProductId, DateTime DeletedAtUtc);
