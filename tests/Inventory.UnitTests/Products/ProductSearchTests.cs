using Inventory.Application.Products;

namespace Inventory.UnitTests.Products;

public sealed class ProductSearchTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ReturnsNull_WhenSearchIsMissingOrWhitespace(string? search)
    {
        Assert.Null(ProductSearch.Normalize(search));
    }

    [Fact]
    public void Normalize_TrimsSurroundingSpaces()
    {
        Assert.Equal("torn", ProductSearch.Normalize(" torn "));
    }

    [Fact]
    public void Normalize_KeepsWildcardCharactersUnchanged()
    {
        Assert.Equal("50%", ProductSearch.Normalize("50%"));
    }
}
