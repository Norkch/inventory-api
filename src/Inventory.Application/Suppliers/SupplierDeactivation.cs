namespace Inventory.Application.Suppliers;

public sealed record SupplierDeactivation(Guid SupplierId, DateTime DeactivatedAtUtc);
