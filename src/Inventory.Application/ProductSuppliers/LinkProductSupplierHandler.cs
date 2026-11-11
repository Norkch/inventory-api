using Inventory.Application.Abstractions;
using Inventory.Application.Products;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.ProductSuppliers;

public sealed class LinkProductSupplierHandler
{
    private readonly IProductSupplierWriteRepository _productSupplierWriteRepository;
    private readonly IClock _clock;

    public LinkProductSupplierHandler(IProductSupplierWriteRepository productSupplierWriteRepository, IClock clock)
    {
        _productSupplierWriteRepository = productSupplierWriteRepository;
        _clock = clock;
    }

    public async Task<ProductSupplierDto> HandleAsync(LinkProductSupplierCommand command, CancellationToken cancellationToken)
    {
        var link = ProductSupplier.Create(CreateDraft(command));
        var result = await _productSupplierWriteRepository.LinkAsync(link, cancellationToken);

        return EnsureLinked(result, link);
    }

    private ProductSupplierDraft CreateDraft(LinkProductSupplierCommand command)
    {
        return new ProductSupplierDraft(command.ProductId, command.SupplierId, command.SupplierSku, _clock.UtcNow);
    }

    private static ProductSupplierDto EnsureLinked(ProductSupplierLinkResult result, ProductSupplier link)
    {
        return result.Status switch
        {
            ProductSupplierLinkStatus.Linked => result.Link!,
            ProductSupplierLinkStatus.ProductNotFound => throw new NotFoundException(ProductMessages.ActiveProductNotFound(link.ProductId)),
            ProductSupplierLinkStatus.SupplierNotAvailable => throw new ValidationException(ProductSupplierMessages.SupplierNotAvailable),
            ProductSupplierLinkStatus.AlreadyLinked => throw new ConflictException(ProductSupplierMessages.AlreadyLinked),
            _ => throw new InvalidOperationException($"Unknown product supplier link status '{result.Status}'.")
        };
    }
}
