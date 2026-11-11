using Inventory.Application.Common;
using Inventory.Application.ProductSuppliers;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeProductSupplierReadRepository : IProductSupplierReadRepository
{
    public List<ProductSupplierDto> VisibleLinks { get; } = new();

    public List<Guid> ActiveProductIds { get; } = new();

    public List<Guid> ActiveSupplierIds { get; } = new();

    public List<string> Calls { get; } = new();

    public Task<ProductSupplierDto?> FindVisibleAsync(ProductSupplierPair pair, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(FindVisibleAsync));

        return Task.FromResult(VisibleLinks.FirstOrDefault(link => link.ProductId == pair.ProductId && link.SupplierId == pair.SupplierId));
    }

    public Task<bool> ExistsActiveProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ExistsActiveProductAsync));

        return Task.FromResult(ActiveProductIds.Contains(productId));
    }

    public Task<bool> ExistsActiveSupplierAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ExistsActiveSupplierAsync));

        return Task.FromResult(ActiveSupplierIds.Contains(supplierId));
    }

    public Task<PagedResult<ProductSupplierDto>> ListByProductAsync(ProductSuppliersCriteria criteria, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ListByProductAsync));
        var items = VisibleLinks.Where(link => link.ProductId == criteria.ProductId).ToList();

        return Task.FromResult(new PagedResult<ProductSupplierDto>(items, criteria.PageRequest, items.Count));
    }

    public Task<PagedResult<ProductSupplierDto>> ListBySupplierAsync(SupplierProductsCriteria criteria, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ListBySupplierAsync));
        var items = VisibleLinks.Where(link => link.SupplierId == criteria.SupplierId).ToList();

        return Task.FromResult(new PagedResult<ProductSupplierDto>(items, criteria.PageRequest, items.Count));
    }
}
