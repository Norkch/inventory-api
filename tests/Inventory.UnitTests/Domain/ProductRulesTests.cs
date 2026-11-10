using Inventory.Domain;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class ProductRulesTests
{
    [Fact]
    public void BR01_ValidateSku_TrimsSurroundingSpaces()
    {
        Assert.Equal("TOR-001", ProductRules.ValidateSku("  TOR-001  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR01_ValidateSku_Rejects_MissingOrWhitespaceSku(string? sku)
    {
        var exception = Assert.Throws<ValidationException>(() => ProductRules.ValidateSku(sku));
        Assert.Equal("El SKU del producto es obligatorio.", exception.Message);
    }

    [Fact]
    public void BR01_ValidateSku_Accepts_SkuOf50CharactersAfterTrimming()
    {
        var sku = new string('A', 50);
        Assert.Equal(sku, ProductRules.ValidateSku($" {sku} "));
    }

    [Fact]
    public void BR01_ValidateSku_Rejects_SkuOf51Characters()
    {
        var exception = Assert.Throws<ValidationException>(() => ProductRules.ValidateSku(new string('A', 51)));
        Assert.Equal("El SKU del producto no puede superar los 50 caracteres.", exception.Message);
    }

    [Fact]
    public void BR02_ValidateName_TrimsSurroundingSpaces()
    {
        Assert.Equal("Tornillo 3/8", ProductRules.ValidateName("  Tornillo 3/8  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR02_ValidateName_Rejects_MissingOrWhitespaceName(string? name)
    {
        var exception = Assert.Throws<ValidationException>(() => ProductRules.ValidateName(name));
        Assert.Equal("El nombre del producto es obligatorio.", exception.Message);
    }

    [Fact]
    public void BR02_ValidateName_Accepts_NameOf200Characters()
    {
        var name = new string('a', 200);
        Assert.Equal(name, ProductRules.ValidateName(name));
    }

    [Fact]
    public void BR02_ValidateName_Rejects_NameOf201Characters()
    {
        var exception = Assert.Throws<ValidationException>(() => ProductRules.ValidateName(new string('a', 201)));
        Assert.Equal("El nombre del producto no puede superar los 200 caracteres.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateDescription_ReturnsNull_WhenDescriptionIsMissingOrWhitespace(string? description)
    {
        Assert.Null(ProductRules.ValidateDescription(description));
    }

    [Fact]
    public void ValidateDescription_TrimsSurroundingSpaces()
    {
        Assert.Equal("Acero inoxidable", ProductRules.ValidateDescription("  Acero inoxidable  "));
    }

    [Fact]
    public void ValidateDescription_Accepts_DescriptionOf1000Characters()
    {
        var description = new string('d', 1000);
        Assert.Equal(description, ProductRules.ValidateDescription(description));
    }

    [Fact]
    public void ValidateDescription_Rejects_DescriptionOf1001Characters()
    {
        var exception = Assert.Throws<ValidationException>(() => ProductRules.ValidateDescription(new string('d', 1001)));
        Assert.Equal("La descripción del producto no puede superar los 1000 caracteres.", exception.Message);
    }

    [Fact]
    public void BR03_ValidateCategoryId_ReturnsTheId_WhenItIsPresent()
    {
        var categoryId = Guid.NewGuid();
        Assert.Equal(categoryId, ProductRules.ValidateCategoryId(categoryId));
    }

    [Fact]
    public void BR03_ValidateCategoryId_Rejects_MissingCategoryId()
    {
        var exception = Assert.Throws<ValidationException>(() => ProductRules.ValidateCategoryId(null));
        Assert.Equal("La categoría del producto es obligatoria.", exception.Message);
    }

    [Fact]
    public void BR03_ValidateCategoryId_Rejects_EmptyCategoryId()
    {
        var exception = Assert.Throws<ValidationException>(() => ProductRules.ValidateCategoryId(Guid.Empty));
        Assert.Equal("La categoría del producto es obligatoria.", exception.Message);
    }
}
