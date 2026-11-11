namespace Inventory.Domain.Entities;

public sealed record SupplierSnapshot(
    Guid Id,
    string Name,
    string Nit,
    string? Email,
    string? Phone,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
