namespace Inventory.Application.Suppliers;

public sealed record UpdateSupplierCommand(Guid Id, string? Name, string? Email, string? Phone);
