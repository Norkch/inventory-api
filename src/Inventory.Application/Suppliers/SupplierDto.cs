using Inventory.Domain.Entities;

namespace Inventory.Application.Suppliers;

public sealed record SupplierDto(
    Guid Id,
    string Name,
    string Nit,
    string? Email,
    string? Phone,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc)
{
    public static SupplierDto From(Supplier supplier)
    {
        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.Nit,
            supplier.Email,
            supplier.Phone,
            supplier.CreatedAtUtc,
            supplier.UpdatedAtUtc);
    }
}
