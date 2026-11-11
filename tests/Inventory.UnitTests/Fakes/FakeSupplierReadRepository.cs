using Inventory.Application.Common;
using Inventory.Application.Suppliers;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeSupplierReadRepository : ISupplierReadRepository
{
    public List<SupplierDto> ActiveSuppliers { get; } = new();

    public SupplierListCriteria? ReceivedCriteria { get; private set; }

    public Task<SupplierDto?> FindActiveAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        return Task.FromResult(ActiveSuppliers.FirstOrDefault(supplier => supplier.Id == supplierId));
    }

    public Task<PagedResult<SupplierDto>> ListAsync(SupplierListCriteria criteria, CancellationToken cancellationToken)
    {
        ReceivedCriteria = criteria;

        return Task.FromResult(new PagedResult<SupplierDto>(ActiveSuppliers, criteria.PageRequest, ActiveSuppliers.Count));
    }
}
