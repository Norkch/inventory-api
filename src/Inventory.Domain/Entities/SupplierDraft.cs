namespace Inventory.Domain.Entities;

public sealed record SupplierDraft(
    string? Name,
    string? Nit,
    string? Email,
    string? Phone,
    DateTime CreatedAtUtc);
