using Inventory.Application.Abstractions;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.Suppliers;

public sealed class DeactivateSupplierHandler
{
    private readonly ISupplierWriteRepository _supplierWriteRepository;
    private readonly IClock _clock;

    public DeactivateSupplierHandler(ISupplierWriteRepository supplierWriteRepository, IClock clock)
    {
        _supplierWriteRepository = supplierWriteRepository;
        _clock = clock;
    }

    public async Task HandleAsync(DeactivateSupplierCommand command, CancellationToken cancellationToken)
    {
        var deactivation = new SupplierDeactivation(command.Id, _clock.UtcNow);
        if (!await _supplierWriteRepository.DeactivateAsync(deactivation, cancellationToken))
        {
            throw new NotFoundException(SupplierMessages.ActiveSupplierNotFound(command.Id));
        }
    }
}
