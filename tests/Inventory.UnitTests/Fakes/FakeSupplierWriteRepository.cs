using Inventory.Application.Suppliers;
using Inventory.Domain.Entities;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeSupplierWriteRepository : ISupplierWriteRepository
{
    public List<string> ExistingNits { get; } = new();

    public List<Guid> ActiveSupplierIds { get; } = new();

    public List<Supplier> Created { get; } = new();

    public List<string> Calls { get; } = new();

    public Exception? CreateException { get; set; }

    public Supplier? UpdateResult { get; set; }

    public SupplierChanges? AppliedChanges { get; private set; }

    public SupplierDeactivation? AppliedDeactivation { get; private set; }

    public Task<bool> ExistsWithSameNitAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ExistsWithSameNitAsync));

        return Task.FromResult(ExistingNits.Contains(supplier.Nit, StringComparer.OrdinalIgnoreCase));
    }

    public Task CreateAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(CreateAsync));
        if (CreateException is not null)
        {
            throw CreateException;
        }
        Created.Add(supplier);

        return Task.CompletedTask;
    }

    public Task<Supplier?> UpdateAsync(SupplierChanges changes, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(UpdateAsync));
        AppliedChanges = changes;

        return Task.FromResult(UpdateResult);
    }

    public Task<bool> DeactivateAsync(SupplierDeactivation deactivation, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(DeactivateAsync));
        AppliedDeactivation = deactivation;

        return Task.FromResult(ActiveSupplierIds.Remove(deactivation.SupplierId));
    }
}
