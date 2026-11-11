using Inventory.Application.ProductSuppliers;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.ProductSuppliers;

public sealed class ListSupplierProductsHandlerTests
{
    private static readonly Guid SupplierId = Guid.NewGuid();

    private readonly FakeProductSupplierReadRepository _readRepository = new();

    [Fact]
    public async Task BR18_Rejects_InvalidPagination_BeforeCheckingTheSupplier()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(new ListSupplierProductsQuery(SupplierId, null, 0), CancellationToken.None));
        Assert.Empty(_readRepository.Calls);
    }

    [Fact]
    public async Task BR09_Rejects_SupplierThatIsNotActive_WithoutListing()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(new ListSupplierProductsQuery(SupplierId, null, null), CancellationToken.None));
        Assert.Equal($"No existe un proveedor activo con el id '{SupplierId}'.", exception.Message);
        Assert.Equal(new[] { nameof(FakeProductSupplierReadRepository.ExistsActiveSupplierAsync) }, _readRepository.Calls);
    }

    [Fact]
    public async Task BR18_Returns_TheProductsOfTheActiveSupplier()
    {
        _readRepository.ActiveSupplierIds.Add(SupplierId);
        _readRepository.VisibleLinks.Add(new ProductSupplierDto(Guid.NewGuid(), "Tornillo 3/8", SupplierId, "Ferretería Andina", null, FakeClock.FixedUtcNow));
        var result = await CreateHandler().HandleAsync(new ListSupplierProductsQuery(SupplierId, 1, 10), CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task BR18_Returns_AnEmptyPage_WhenActiveSupplierHasNoProducts()
    {
        _readRepository.ActiveSupplierIds.Add(SupplierId);
        var result = await CreateHandler().HandleAsync(new ListSupplierProductsQuery(SupplierId, null, null), CancellationToken.None);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    private ListSupplierProductsHandler CreateHandler()
    {
        return new ListSupplierProductsHandler(_readRepository);
    }
}
