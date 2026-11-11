namespace Inventory.Application.ProductSuppliers;

public sealed record ListProductSuppliersQuery(Guid ProductId, int? Page, int? PageSize);
