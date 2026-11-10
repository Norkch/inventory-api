using Inventory.Application.Products;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Products;

public sealed class GetProductByIdHandlerTests
{
    private readonly FakeProductReadRepository _readRepository = new();

    [Fact]
    public async Task Returns_TheActiveProduct()
    {
        var expected = new ProductDto(Guid.NewGuid(), "TOR-001", "Tornillo", null, Guid.NewGuid(), "Tornillería", 0, FakeClock.FixedUtcNow, null);
        _readRepository.ActiveProducts.Add(expected);
        var product = await CreateHandler().HandleAsync(new GetProductByIdQuery(expected.Id), CancellationToken.None);
        Assert.Equal(expected, product);
    }

    [Fact]
    public async Task Rejects_IdWithoutActiveProduct()
    {
        var productId = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(new GetProductByIdQuery(productId), CancellationToken.None));
        Assert.Equal($"No existe un producto activo con el id '{productId}'.", exception.Message);
    }

    private GetProductByIdHandler CreateHandler()
    {
        return new GetProductByIdHandler(_readRepository);
    }
}
