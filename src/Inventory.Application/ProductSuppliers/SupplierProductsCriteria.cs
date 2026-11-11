using Inventory.Application.Common;

namespace Inventory.Application.ProductSuppliers;

public sealed record SupplierProductsCriteria(Guid SupplierId, PageRequest PageRequest);
