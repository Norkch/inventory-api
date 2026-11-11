namespace Inventory.Application.Suppliers;

public sealed record CreateSupplierCommand(string? Name, string? Nit, string? Email, string? Phone);
