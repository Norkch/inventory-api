using Inventory.Application.Products;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Products;

public sealed class CreateProductHandlerTests
{
    private static readonly CategoryReference Hardware = new(Guid.NewGuid(), "Tornillería");

    private readonly FakeProductWriteRepository _writeRepository = new();

    public CreateProductHandlerTests()
    {
        _writeRepository.ActiveCategories.Add(Hardware);
    }

    [Fact]
    public async Task BR07_Creates_ActiveProductWithZeroStockAndClockDate()
    {
        var product = await CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);
        var stored = Assert.Single(_writeRepository.Added);
        Assert.Equal(stored.Id, product.Id);
        Assert.True(stored.IsActive);
        Assert.Equal("TOR-001", product.Sku);
        Assert.Equal(0, product.StockQuantity);
        Assert.Equal(FakeClock.FixedUtcNow, product.CreatedAtUtc);
        Assert.Null(product.UpdatedAtUtc);
    }

    [Fact]
    public async Task BR03_Returns_TheCategoryName()
    {
        var product = await CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);
        Assert.Equal(Hardware.Id, product.CategoryId);
        Assert.Equal("Tornillería", product.CategoryName);
    }

    [Fact]
    public async Task BR03_Rejects_CategoryThatIsNotActive()
    {
        var command = ValidCommand() with { CategoryId = Guid.NewGuid() };
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal(ProductMessages.CategoryNotActive, exception.Message);
        Assert.Empty(_writeRepository.Added);
    }

    [Theory]
    [InlineData("tor-001")]
    [InlineData(" TOR-001 ")]
    public async Task BR01_Rejects_SkuOfAnotherProduct(string sku)
    {
        _writeRepository.ExistingSkus.Add("TOR-001");
        var command = ValidCommand() with { Sku = sku };
        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal($"Ya existe un producto con el SKU '{sku.Trim()}'.", exception.Message);
        Assert.Empty(_writeRepository.Added);
    }

    [Fact]
    public async Task BR03_Rejects_CategoryDeactivatedBeforeTheInsert()
    {
        _writeRepository.AddSucceeds = false;
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None));
        Assert.Equal(ProductMessages.CategoryNotActive, exception.Message);
    }

    [Fact]
    public async Task BR02_RejectsInvalidName_WithoutCheckingTheCategory()
    {
        var command = ValidCommand() with { Name = "", CategoryId = Guid.NewGuid() };
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal("El nombre del producto es obligatorio.", exception.Message);
        Assert.Empty(_writeRepository.Calls);
    }

    [Fact]
    public async Task BR03_RejectsInactiveCategory_WithoutCheckingTheSku()
    {
        _writeRepository.ExistingSkus.Add("TOR-001");
        var command = ValidCommand() with { CategoryId = Guid.NewGuid() };
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal(new[] { nameof(IProductWriteRepository.FindActiveCategoryAsync) }, _writeRepository.Calls);
    }

    private static CreateProductCommand ValidCommand()
    {
        return new CreateProductCommand("TOR-001", "Tornillo 3/8", "Acero inoxidable", Hardware.Id);
    }

    private CreateProductHandler CreateHandler()
    {
        return new CreateProductHandler(_writeRepository, new FakeClock());
    }
}
