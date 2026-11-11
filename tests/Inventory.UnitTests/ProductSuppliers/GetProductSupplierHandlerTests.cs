using Inventory.Application.ProductSuppliers;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.ProductSuppliers;

public sealed class GetProductSupplierHandlerTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private readonly FakeProductSupplierReadRepository _readRepository = new();

    [Fact]
    public async Task Returns_VisibleLink()
    {
        var visible = new ProductSupplierDto(ProductId, "Tornillo 3/8", SupplierId, "Ferretería Andina", "FA-778", FakeClock.FixedUtcNow);
        _readRepository.VisibleLinks.Add(visible);
        var link = await CreateHandler().HandleAsync(new GetProductSupplierQuery(ProductId, SupplierId), CancellationToken.None);
        Assert.Equal(visible, link);
    }

    [Fact]
    public async Task Rejects_PairThatIsNotVisible()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(new GetProductSupplierQuery(ProductId, SupplierId), CancellationToken.None));
        Assert.Equal(ProductSupplierMessages.NotLinked, exception.Message);
    }

    private GetProductSupplierHandler CreateHandler()
    {
        return new GetProductSupplierHandler(_readRepository);
    }
}
