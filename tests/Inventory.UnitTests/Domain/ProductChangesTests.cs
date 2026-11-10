using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class ProductChangesTests
{
    private static readonly DateTime UpdatedAtUtc = new(2026, 11, 10, 11, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    [Fact]
    public void Create_ValidatesAndTrimsEveryField()
    {
        var changes = ProductChanges.Create(ProductId, " Tornillo 1/2 ", "   ", CategoryId, UpdatedAtUtc);
        Assert.Equal(ProductId, changes.ProductId);
        Assert.Equal("Tornillo 1/2", changes.Name);
        Assert.Null(changes.Description);
        Assert.Equal(CategoryId, changes.CategoryId);
        Assert.Equal(UpdatedAtUtc, changes.UpdatedAtUtc);
    }

    [Fact]
    public void BR02_Create_Rejects_MissingName()
    {
        var exception = Assert.Throws<ValidationException>(() => ProductChanges.Create(ProductId, "", null, CategoryId, UpdatedAtUtc));
        Assert.Equal("El nombre del producto es obligatorio.", exception.Message);
    }

    [Fact]
    public void Create_Rejects_DescriptionOf1001Characters()
    {
        var exception = Assert.Throws<ValidationException>(() => ProductChanges.Create(ProductId, "Tornillo", new string('d', 1001), CategoryId, UpdatedAtUtc));
        Assert.Equal("La descripción del producto no puede superar los 1000 caracteres.", exception.Message);
    }

    [Fact]
    public void BR03_Create_Rejects_MissingCategory()
    {
        var exception = Assert.Throws<ValidationException>(() => ProductChanges.Create(ProductId, "Tornillo", null, null, UpdatedAtUtc));
        Assert.Equal("La categoría del producto es obligatoria.", exception.Message);
    }

    [Theory]
    [InlineData("Sku")]
    [InlineData("StockQuantity")]
    public void BR01_BR07_ProductChanges_HasNoSkuOrStock(string propertyName)
    {
        Assert.Null(typeof(ProductChanges).GetProperty(propertyName));
    }
}
