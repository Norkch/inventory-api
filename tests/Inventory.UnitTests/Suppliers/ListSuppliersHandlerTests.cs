using Inventory.Application.Suppliers;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Suppliers;

public sealed class ListSuppliersHandlerTests
{
    private readonly FakeSupplierReadRepository _readRepository = new();

    [Theory]
    [InlineData(0, null)]
    [InlineData(null, 101)]
    public async Task BR18_Rejects_InvalidPagination_WithoutQuerying(int? page, int? pageSize)
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(new ListSuppliersQuery(page, pageSize), CancellationToken.None));
        Assert.Null(_readRepository.ReceivedCriteria);
    }

    [Fact]
    public async Task BR18_Returns_ThePageFromTheRepository_WithDefaultPagination()
    {
        _readRepository.ActiveSuppliers.Add(new SupplierDto(Guid.NewGuid(), "Andina", "900123456-7", null, null, FakeClock.FixedUtcNow, null));
        var result = await CreateHandler().HandleAsync(new ListSuppliersQuery(null, null), CancellationToken.None);
        Assert.Equal(1, _readRepository.ReceivedCriteria!.PageRequest.Page);
        Assert.Equal(20, _readRepository.ReceivedCriteria.PageRequest.PageSize);
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    private ListSuppliersHandler CreateHandler()
    {
        return new ListSuppliersHandler(_readRepository);
    }
}
