using Inventory.Application.Products;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Products;

public sealed class DeleteProductHandlerTests
{
    private static readonly Guid ProductId = Guid.NewGuid();

    private readonly FakeProductWriteRepository _writeRepository = new();

    [Fact]
    public async Task BR06_Deletes_ActiveProductWithTheClockDate()
    {
        _writeRepository.ActiveProductIds.Add(ProductId);
        await CreateHandler().HandleAsync(new DeleteProductCommand(ProductId), CancellationToken.None);
        Assert.Equal(new ProductDeletion(ProductId, FakeClock.FixedUtcNow), _writeRepository.AppliedDeletion);
        Assert.Empty(_writeRepository.ActiveProductIds);
    }

    [Fact]
    public async Task BR06_Rejects_ProductThatIsNotActive()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(new DeleteProductCommand(ProductId), CancellationToken.None));
        Assert.Equal($"No existe un producto activo con el id '{ProductId}'.", exception.Message);
    }

    private DeleteProductHandler CreateHandler()
    {
        return new DeleteProductHandler(_writeRepository, new FakeClock());
    }
}
