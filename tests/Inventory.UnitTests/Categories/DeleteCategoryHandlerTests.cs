using Inventory.Application.Categories;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Categories;

public sealed class DeleteCategoryHandlerTests
{
    private static readonly Guid CategoryId = Guid.Parse("6f1c2b8e-3d4a-4f5b-9c6d-7e8f9a0b1c2d");
    private readonly FakeCategoryWriteRepository _writeRepository = new();

    [Fact]
    public async Task BR05_Deactivates_CategoryWithoutActiveProducts()
    {
        _writeRepository.DeactivationResult = CategoryDeactivationResult.Deactivated;
        await CreateHandler().HandleAsync(new DeleteCategoryCommand(CategoryId), CancellationToken.None);
        Assert.Equal(CategoryId, _writeRepository.DeactivatedCategoryId);
    }

    [Fact]
    public async Task BR05_Rejects_MissingOrInactiveCategory()
    {
        _writeRepository.DeactivationResult = CategoryDeactivationResult.NotFound;
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(new DeleteCategoryCommand(CategoryId), CancellationToken.None));
        Assert.Equal($"No existe una categoría activa con el id '{CategoryId}'.", exception.Message);
    }

    [Fact]
    public async Task BR05_Rejects_CategoryWithActiveProducts()
    {
        _writeRepository.DeactivationResult = CategoryDeactivationResult.HasActiveProducts;
        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler().HandleAsync(new DeleteCategoryCommand(CategoryId), CancellationToken.None));
        Assert.Equal("No se puede eliminar la categoría porque tiene productos activos.", exception.Message);
    }

    private DeleteCategoryHandler CreateHandler()
    {
        return new DeleteCategoryHandler(_writeRepository);
    }
}
