using Inventory.Application.ProductSuppliers;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.ProductSuppliers;

public sealed class ListProductSuppliersHandlerTests
{
    private static readonly Guid ProductId = Guid.NewGuid();

    private readonly FakeProductSupplierReadRepository _readRepository = new();

    [Fact]
    public async Task BR18_Rejects_InvalidPagination_BeforeCheckingTheProduct()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(new ListProductSuppliersQuery(ProductId, 0, null), CancellationToken.None));
        Assert.Empty(_readRepository.Calls);
    }

    [Fact]
    public async Task Rejects_ProductThatIsNotActive_WithoutListing()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(new ListProductSuppliersQuery(ProductId, null, null), CancellationToken.None));
        Assert.Equal($"No existe un producto activo con el id '{ProductId}'.", exception.Message);
        Assert.Equal(new[] { nameof(FakeProductSupplierReadRepository.ExistsActiveProductAsync) }, _readRepository.Calls);
    }

    [Fact]
    public async Task BR18_Returns_TheSuppliersOfTheActiveProduct()
    {
        _readRepository.ActiveProductIds.Add(ProductId);
        _readRepository.VisibleLinks.Add(new ProductSupplierDto(ProductId, "Tornillo 3/8", Guid.NewGuid(), "Ferretería Andina", null, FakeClock.FixedUtcNow));
        var result = await CreateHandler().HandleAsync(new ListProductSuppliersQuery(ProductId, null, null), CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task BR18_Returns_AnEmptyPage_WhenActiveProductHasNoSuppliers()
    {
        _readRepository.ActiveProductIds.Add(ProductId);
        var result = await CreateHandler().HandleAsync(new ListProductSuppliersQuery(ProductId, null, null), CancellationToken.None);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    private ListProductSuppliersHandler CreateHandler()
    {
        return new ListProductSuppliersHandler(_readRepository);
    }
}
