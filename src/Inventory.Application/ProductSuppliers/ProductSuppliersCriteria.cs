using Inventory.Application.Common;

namespace Inventory.Application.ProductSuppliers;

public sealed record ProductSuppliersCriteria(Guid ProductId, PageRequest PageRequest);
