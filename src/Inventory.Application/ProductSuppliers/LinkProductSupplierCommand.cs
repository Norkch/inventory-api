namespace Inventory.Application.ProductSuppliers;

public sealed record LinkProductSupplierCommand(Guid ProductId, Guid? SupplierId, string? SupplierSku);
