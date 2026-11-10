using Inventory.Application.Products;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Products;

public sealed class UpdateProductHandlerTests
{
    private static readonly CategoryReference Fasteners = new(Guid.NewGuid(), "Fijaciones");
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeProductWriteRepository _writeRepository = new();

    public UpdateProductHandlerTests()
    {
        _writeRepository.ActiveCategories.Add(Fasteners);
        _writeRepository.ActiveProductIds.Add(ProductId);
        _writeRepository.UpdateResult = UpdatedProduct();
    }

    [Fact]
    public async Task Updates_TheProductWithTheClockDate()
    {
        var product = await CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);
        var changes = Assert.IsType<ProductChanges>(_writeRepository.AppliedChanges);
        Assert.Equal(ProductId, changes.ProductId);
        Assert.Equal("Tornillo 1/2", changes.Name);
        Assert.Equal(Fasteners.Id, changes.CategoryId);
        Assert.Equal(FakeClock.FixedUtcNow, changes.UpdatedAtUtc);
        Assert.Equal("Fijaciones", product.CategoryName);
        Assert.Equal(FakeClock.FixedUtcNow, product.UpdatedAtUtc);
    }

    [Fact]
    public async Task BR01_BR07_Returns_TheOriginalSkuAndStock()
    {
        var product = await CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None);
        Assert.Equal("TOR-001", product.Sku);
        Assert.Equal(15, product.StockQuantity);
        Assert.Equal(CreatedAtUtc, product.CreatedAtUtc);
    }

    [Fact]
    public async Task Rejects_ProductThatIsNotActive_WithoutCheckingTheCategory()
    {
        _writeRepository.ActiveProductIds.Clear();
        var command = ValidCommand() with { CategoryId = Guid.NewGuid() };
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal($"No existe un producto activo con el id '{ProductId}'.", exception.Message);
        Assert.Equal(new[] { nameof(IProductWriteRepository.ExistsActiveAsync) }, _writeRepository.Calls);
    }

    [Fact]
    public async Task BR03_Rejects_CategoryThatIsNotActive()
    {
        var command = ValidCommand() with { CategoryId = Guid.NewGuid() };
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal(ProductMessages.CategoryNotActive, exception.Message);
        Assert.Null(_writeRepository.AppliedChanges);
    }

    [Fact]
    public async Task BR03_Rejects_CategoryDeactivatedBeforeTheUpdate()
    {
        _writeRepository.UpdateResult = null;
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None));
        Assert.Equal(ProductMessages.CategoryNotActive, exception.Message);
    }

    [Fact]
    public async Task Rejects_ProductDeletedBeforeTheUpdate()
    {
        _writeRepository.UpdateResult = null;
        _writeRepository.DeactivatesProductOnUpdate = true;
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateHandler().HandleAsync(ValidCommand(), CancellationToken.None));
        Assert.Equal($"No existe un producto activo con el id '{ProductId}'.", exception.Message);
    }

    [Fact]
    public async Task BR02_RejectsInvalidName_WithoutCallingTheRepository()
    {
        var command = ValidCommand() with { Name = "" };
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(command, CancellationToken.None));
        Assert.Equal("El nombre del producto es obligatorio.", exception.Message);
        Assert.Empty(_writeRepository.Calls);
    }

    private static UpdateProductCommand ValidCommand()
    {
        return new UpdateProductCommand(ProductId, "Tornillo 1/2", null, Fasteners.Id);
    }

    private static Product UpdatedProduct()
    {
        return Product.Restore(new ProductSnapshot(Id: ProductId, CategoryId: Fasteners.Id, Sku: "TOR-001", Name: "Tornillo 1/2", Description: null, StockQuantity: 15, IsActive: true, CreatedAtUtc: CreatedAtUtc, UpdatedAtUtc: FakeClock.FixedUtcNow));
    }

    private UpdateProductHandler CreateHandler()
    {
        return new UpdateProductHandler(_writeRepository, new FakeClock());
    }
}
