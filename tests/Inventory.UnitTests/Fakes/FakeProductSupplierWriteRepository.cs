using Inventory.Application.ProductSuppliers;
using Inventory.Domain.Entities;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeProductSupplierWriteRepository : IProductSupplierWriteRepository
{
    public ProductSupplierLinkStatus Status { get; set; } = ProductSupplierLinkStatus.Linked;

    public List<string> Calls { get; } = new();

    public ProductSupplier? ReceivedLink { get; private set; }

    public Task<ProductSupplierLinkResult> LinkAsync(ProductSupplier link, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(LinkAsync));
        ReceivedLink = link;

        return Task.FromResult(Status == ProductSupplierLinkStatus.Linked
            ? ProductSupplierLinkResult.Linked(ToDto(link))
            : ProductSupplierLinkResult.Rejected(Status));
    }

    private static ProductSupplierDto ToDto(ProductSupplier link)
    {
        return new ProductSupplierDto(link.ProductId, "Tornillo 3/8", link.SupplierId, "Ferretería Andina", link.SupplierSku, link.CreatedAtUtc);
    }
}
