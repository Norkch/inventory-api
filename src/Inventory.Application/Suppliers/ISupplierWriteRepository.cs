using Inventory.Domain.Entities;

namespace Inventory.Application.Suppliers;

public interface ISupplierWriteRepository
{
    Task<bool> ExistsWithSameNitAsync(Supplier supplier, CancellationToken cancellationToken);

    Task CreateAsync(Supplier supplier, CancellationToken cancellationToken);

    Task<Supplier?> UpdateAsync(SupplierChanges changes, CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(SupplierDeactivation deactivation, CancellationToken cancellationToken);
}
