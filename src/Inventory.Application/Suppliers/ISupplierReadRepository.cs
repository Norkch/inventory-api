using Inventory.Application.Common;

namespace Inventory.Application.Suppliers;

public interface ISupplierReadRepository
{
    Task<SupplierDto?> FindActiveAsync(Guid supplierId, CancellationToken cancellationToken);

    Task<PagedResult<SupplierDto>> ListAsync(SupplierListCriteria criteria, CancellationToken cancellationToken);
}
