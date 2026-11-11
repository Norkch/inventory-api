using Inventory.Application.Abstractions;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.Suppliers;

public sealed class CreateSupplierHandler
{
    private readonly ISupplierWriteRepository _supplierWriteRepository;
    private readonly IClock _clock;

    public CreateSupplierHandler(ISupplierWriteRepository supplierWriteRepository, IClock clock)
    {
        _supplierWriteRepository = supplierWriteRepository;
        _clock = clock;
    }

    public async Task<SupplierDto> HandleAsync(CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        var supplier = Supplier.Create(CreateDraft(command));
        await EnsureNitIsFreeAsync(supplier, cancellationToken);
        await _supplierWriteRepository.CreateAsync(supplier, cancellationToken);

        return SupplierDto.From(supplier);
    }

    private SupplierDraft CreateDraft(CreateSupplierCommand command)
    {
        return new SupplierDraft(command.Name, command.Nit, command.Email, command.Phone, _clock.UtcNow);
    }

    private async Task EnsureNitIsFreeAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        if (await _supplierWriteRepository.ExistsWithSameNitAsync(supplier, cancellationToken))
        {
            throw new ConflictException(SupplierMessages.NitAlreadyExists(supplier));
        }
    }
}
