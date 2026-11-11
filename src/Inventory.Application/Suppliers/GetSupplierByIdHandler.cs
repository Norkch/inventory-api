using Inventory.Domain.Exceptions;

namespace Inventory.Application.Suppliers;

public sealed class GetSupplierByIdHandler
{
    private readonly ISupplierReadRepository _supplierReadRepository;

    public GetSupplierByIdHandler(ISupplierReadRepository supplierReadRepository)
    {
        _supplierReadRepository = supplierReadRepository;
    }

    public async Task<SupplierDto> HandleAsync(GetSupplierByIdQuery query, CancellationToken cancellationToken)
    {
        var supplier = await _supplierReadRepository.FindActiveAsync(query.Id, cancellationToken);

        return supplier ?? throw new NotFoundException(SupplierMessages.ActiveSupplierNotFound(query.Id));
    }
}
