using Inventory.Application.Movements;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Movements;

public sealed class ListMovementsHandlerTests
{
    private static readonly Guid ProductId = Guid.NewGuid();

    private readonly FakeMovementReadRepository _readRepository = new();

    [Fact]
    public async Task BR18_UsesDefaultPage_WhenParametersAreMissing()
    {
        _readRepository.ActiveProductIds.Add(ProductId);
        await CreateHandler().HandleAsync(new ListMovementsQuery(ProductId, null, null), CancellationToken.None);
        Assert.Equal(ProductId, _readRepository.ReceivedCriteria?.ProductId);
        Assert.Equal(1, _readRepository.ReceivedCriteria?.PageRequest.Page);
        Assert.Equal(20, _readRepository.ReceivedCriteria?.PageRequest.PageSize);
    }

    [Fact]
    public async Task BR18_ReturnsTheMovementsOfTheReadPort()
    {
        _readRepository.ActiveProductIds.Add(ProductId);
        _readRepository.Movements.Add(CreateMovement());
        var result = await CreateHandler().HandleAsync(new ListMovementsQuery(ProductId, 1, 10), CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task BR18_ReturnsAnEmptyPage_WhenActiveProductHasNoMovements()
    {
        _readRepository.ActiveProductIds.Add(ProductId);
        var result = await CreateHandler().HandleAsync(new ListMovementsQuery(ProductId, null, null), CancellationToken.None);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Rejects_ProductThatIsNotActiveWithoutListing()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(new ListMovementsQuery(ProductId, null, null), CancellationToken.None));
        Assert.Equal($"No existe un producto activo con el id '{ProductId}'.", exception.Message);
        Assert.DoesNotContain(nameof(FakeMovementReadRepository.ListAsync), _readRepository.Calls);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(null, 101)]
    public async Task BR18_Rejects_InvalidPaginationBeforeCheckingTheProduct(int? page, int? pageSize)
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(new ListMovementsQuery(ProductId, page, pageSize), CancellationToken.None));
        Assert.Empty(_readRepository.Calls);
    }

    private ListMovementsHandler CreateHandler()
    {
        return new ListMovementsHandler(_readRepository);
    }

    private static MovementDto CreateMovement()
    {
        return new MovementDto(Guid.NewGuid(), ProductId, "Entry", 5, 5, null, FakeClock.FixedUtcNow, "3f2a9c1e-0000-4000-8000-000000000001");
    }
}
