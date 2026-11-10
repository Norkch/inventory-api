using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class ProductTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 10, 9, 30, 0, DateTimeKind.Utc);
    private static readonly Guid CategoryId = Guid.NewGuid();

    [Fact]
    public void Create_ValidatesAndTrimsEveryField()
    {
        var product = Product.Create(" TOR-001 ", " Tornillo 3/8 ", " Acero ", CategoryId, CreatedAtUtc);
        Assert.Equal("TOR-001", product.Sku);
        Assert.Equal("Tornillo 3/8", product.Name);
        Assert.Equal("Acero", product.Description);
        Assert.Equal(CategoryId, product.CategoryId);
    }

    [Fact]
    public void BR07_Create_StartsWithZeroStock()
    {
        var product = Product.Create("TOR-001", "Tornillo", null, CategoryId, CreatedAtUtc);
        Assert.Equal(0, product.StockQuantity);
    }

    [Fact]
    public void Create_IsActive_WithCreationDateAndNoUpdateDate()
    {
        var product = Product.Create("TOR-001", "Tornillo", null, CategoryId, CreatedAtUtc);
        Assert.True(product.IsActive);
        Assert.Equal(CreatedAtUtc, product.CreatedAtUtc);
        Assert.Null(product.UpdatedAtUtc);
        Assert.NotEqual(Guid.Empty, product.Id);
    }

    [Fact]
    public void BR01_Create_Rejects_MissingSku()
    {
        var exception = Assert.Throws<ValidationException>(() => Product.Create(null, "Tornillo", null, CategoryId, CreatedAtUtc));
        Assert.Equal("El SKU del producto es obligatorio.", exception.Message);
    }

    [Fact]
    public void BR02_Create_Rejects_MissingName()
    {
        var exception = Assert.Throws<ValidationException>(() => Product.Create("TOR-001", " ", null, CategoryId, CreatedAtUtc));
        Assert.Equal("El nombre del producto es obligatorio.", exception.Message);
    }

    [Fact]
    public void BR03_Create_Rejects_MissingCategory()
    {
        var exception = Assert.Throws<ValidationException>(() => Product.Create("TOR-001", "Tornillo", null, null, CreatedAtUtc));
        Assert.Equal("La categoría del producto es obligatoria.", exception.Message);
    }

    [Fact]
    public void Create_ReportsTheNameBeforeTheCategory_WhenBothAreMissing()
    {
        var exception = Assert.Throws<ValidationException>(() => Product.Create("TOR-001", "", null, null, CreatedAtUtc));
        Assert.Equal("El nombre del producto es obligatorio.", exception.Message);
    }

    [Fact]
    public void Restore_KeepsEverySnapshotValue()
    {
        var snapshot = new ProductSnapshot(Id: Guid.NewGuid(), CategoryId: CategoryId, Sku: "TOR-001", Name: "Tornillo", Description: "Acero", StockQuantity: 15, IsActive: false, CreatedAtUtc: CreatedAtUtc, UpdatedAtUtc: CreatedAtUtc.AddHours(1));
        var product = Product.Restore(snapshot);
        Assert.Equal(snapshot.Id, product.Id);
        Assert.Equal(snapshot.CategoryId, product.CategoryId);
        Assert.Equal(snapshot.Sku, product.Sku);
        Assert.Equal(snapshot.Name, product.Name);
        Assert.Equal(snapshot.Description, product.Description);
        Assert.Equal(15, product.StockQuantity);
        Assert.False(product.IsActive);
        Assert.Equal(snapshot.CreatedAtUtc, product.CreatedAtUtc);
        Assert.Equal(snapshot.UpdatedAtUtc, product.UpdatedAtUtc);
    }
}
