using Inventory.Application.Categories;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Categories;

public sealed class GetCategoryByIdHandlerTests
{
    private static readonly CategoryDto Tornilleria = new(Guid.Parse("6f1c2b8e-3d4a-4f5b-9c6d-7e8f9a0b1c2d"), "Tornillería", null, FakeClock.FixedUtcNow);
    private readonly FakeCategoryReadRepository _readRepository = new();

    [Fact]
    public async Task Returns_ActiveCategory()
    {
        _readRepository.ActiveCategories.Add(Tornilleria);
        var category = await CreateHandler().HandleAsync(new GetCategoryByIdQuery(Tornilleria.Id), CancellationToken.None);
        Assert.Equal(Tornilleria, category);
    }

    [Fact]
    public async Task Rejects_IdWithoutActiveCategory()
    {
        var missingId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(new GetCategoryByIdQuery(missingId), CancellationToken.None));
        Assert.Equal($"No existe una categoría activa con el id '{missingId}'.", exception.Message);
    }

    private GetCategoryByIdHandler CreateHandler()
    {
        return new GetCategoryByIdHandler(_readRepository);
    }
}
