using Inventory.Application.Categories;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Categories;

public sealed class CreateCategoryHandlerTests
{
    private readonly FakeCategoryWriteRepository _writeRepository = new();

    [Fact]
    public async Task BR04_Creates_ActiveCategoryWithClockDate()
    {
        var category = await CreateHandler().HandleAsync(new CreateCategoryCommand("Tornillería", "Tornillos y tuercas"), CancellationToken.None);
        var stored = Assert.Single(_writeRepository.Added);
        Assert.Equal(stored.Id, category.Id);
        Assert.True(stored.IsActive);
        Assert.Equal("Tornillería", category.Name);
        Assert.Equal("Tornillos y tuercas", category.Description);
        Assert.Equal(FakeClock.FixedUtcNow, category.CreatedAtUtc);
    }

    [Fact]
    public async Task BR04_Creates_CategoryWithoutDescription()
    {
        var category = await CreateHandler().HandleAsync(new CreateCategoryCommand("Tornillería", null), CancellationToken.None);
        Assert.Null(category.Description);
    }

    [Theory]
    [InlineData("TORNILLERÍA")]
    [InlineData(" Tornillería ")]
    public async Task BR04_Rejects_NameOfAnotherActiveCategory(string name)
    {
        _writeRepository.ActiveNames.Add("Tornillería");
        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler().HandleAsync(new CreateCategoryCommand(name, null), CancellationToken.None));
        Assert.Equal($"Ya existe una categoría con el nombre '{name.Trim()}'.", exception.Message);
        Assert.Empty(_writeRepository.Added);
    }

    [Fact]
    public async Task BR04_Rejects_InvalidCategoryWithoutSavingIt()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(new CreateCategoryCommand("   ", null), CancellationToken.None));
        Assert.Empty(_writeRepository.Added);
    }

    private CreateCategoryHandler CreateHandler()
    {
        return new CreateCategoryHandler(_writeRepository, new FakeClock());
    }
}
