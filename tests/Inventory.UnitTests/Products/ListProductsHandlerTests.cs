using Inventory.Application.Products;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Products;

public sealed class ListProductsHandlerTests
{
    private readonly FakeProductReadRepository _readRepository = new();

    [Fact]
    public async Task BR18_UsesDefaultPage_WhenParametersAreMissing()
    {
        await CreateHandler().HandleAsync(new ListProductsQuery(null, null, null, null), CancellationToken.None);
        Assert.Equal(1, _readRepository.ReceivedCriteria?.PageRequest.Page);
        Assert.Equal(20, _readRepository.ReceivedCriteria?.PageRequest.PageSize);
        Assert.Null(_readRepository.ReceivedCriteria?.CategoryId);
        Assert.Null(_readRepository.ReceivedCriteria?.Search);
    }

    [Fact]
    public async Task PassesFiltersAndNormalizedSearchToReadPort()
    {
        var categoryId = Guid.NewGuid();
        await CreateHandler().HandleAsync(new ListProductsQuery(2, 10, categoryId, "  torn  "), CancellationToken.None);
        Assert.Equal(2, _readRepository.ReceivedCriteria?.PageRequest.Page);
        Assert.Equal(10, _readRepository.ReceivedCriteria?.PageRequest.PageSize);
        Assert.Equal(categoryId, _readRepository.ReceivedCriteria?.CategoryId);
        Assert.Equal("torn", _readRepository.ReceivedCriteria?.Search);
    }

    [Fact]
    public async Task TreatsWhitespaceSearchAsMissing()
    {
        await CreateHandler().HandleAsync(new ListProductsQuery(null, null, null, "   "), CancellationToken.None);
        Assert.Null(_readRepository.ReceivedCriteria?.Search);
    }

    [Fact]
    public async Task BR18_Rejects_InvalidParametersBeforeQueryingReadPort()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(new ListProductsQuery(1, 101, null, null), CancellationToken.None));
        Assert.Null(_readRepository.ReceivedCriteria);
    }

    [Fact]
    public async Task Returns_ThePageFromTheReadPort()
    {
        _readRepository.ActiveProducts.Add(new ProductDto(Guid.NewGuid(), "TOR-001", "Tornillo", null, Guid.NewGuid(), "Tornillería", 0, FakeClock.FixedUtcNow, null));
        var result = await CreateHandler().HandleAsync(new ListProductsQuery(null, null, null, null), CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    private ListProductsHandler CreateHandler()
    {
        return new ListProductsHandler(_readRepository);
    }
}
