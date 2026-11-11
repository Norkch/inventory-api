namespace Inventory.Application.ProductSuppliers;

public sealed record GetProductSupplierQuery(Guid ProductId, Guid SupplierId);
