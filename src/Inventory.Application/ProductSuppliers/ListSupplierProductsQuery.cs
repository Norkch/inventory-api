namespace Inventory.Application.ProductSuppliers;

public sealed record ListSupplierProductsQuery(Guid SupplierId, int? Page, int? PageSize);
