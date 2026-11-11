using Inventory.Application.Common;

namespace Inventory.Application.Suppliers;

public sealed class ListSuppliersHandler
{
    private readonly ISupplierReadRepository _supplierReadRepository;

    public ListSuppliersHandler(ISupplierReadRepository supplierReadRepository)
    {
        _supplierReadRepository = supplierReadRepository;
    }

    public Task<PagedResult<SupplierDto>> HandleAsync(ListSuppliersQuery query, CancellationToken cancellationToken)
    {
        var criteria = new SupplierListCriteria(PageRequest.Create(query.Page, query.PageSize));

        return _supplierReadRepository.ListAsync(criteria, cancellationToken);
    }
}
