using Inventory.Application.Abstractions;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.Suppliers;

public sealed class UpdateSupplierHandler
{
    private readonly ISupplierWriteRepository _supplierWriteRepository;
    private readonly IClock _clock;

    public UpdateSupplierHandler(ISupplierWriteRepository supplierWriteRepository, IClock clock)
    {
        _supplierWriteRepository = supplierWriteRepository;
        _clock = clock;
    }

    public async Task<SupplierDto> HandleAsync(UpdateSupplierCommand command, CancellationToken cancellationToken)
    {
        var changes = SupplierChanges.Create(CreateEdit(command));
        var supplier = await UpdateActiveSupplierAsync(changes, cancellationToken);

        return SupplierDto.From(supplier);
    }

    private async Task<Supplier> UpdateActiveSupplierAsync(SupplierChanges changes, CancellationToken cancellationToken)
    {
        var supplier = await _supplierWriteRepository.UpdateAsync(changes, cancellationToken);

        return supplier ?? throw new NotFoundException(SupplierMessages.ActiveSupplierNotFound(changes.SupplierId));
    }

    private SupplierEdit CreateEdit(UpdateSupplierCommand command)
    {
        return new SupplierEdit(command.Id, command.Name, command.Email, command.Phone, _clock.UtcNow);
    }
}
