using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class CategoryTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 9, 14, 3, 0, DateTimeKind.Utc);

    [Fact]
    public void BR04_Create_TrimsName_WhenNameHasSurroundingSpaces()
    {
        var category = Category.Create("  Tornillería  ", null, CreatedAtUtc);
        Assert.Equal("Tornillería", category.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("     ")]
    public void BR04_Create_Rejects_MissingOrWhitespaceName(string? name)
    {
        var exception = Assert.Throws<ValidationException>(() => Category.Create(name, null, CreatedAtUtc));
        Assert.Equal("El nombre de la categoría es obligatorio.", exception.Message);
    }

    [Fact]
    public void BR04_Create_Accepts_NameOf100Characters()
    {
        var name = new string('a', 100);
        var category = Category.Create(name, null, CreatedAtUtc);
        Assert.Equal(name, category.Name);
    }

    [Fact]
    public void BR04_Create_Rejects_NameOf101CharactersAfterTrimming()
    {
        var name = $"  {new string('a', 101)}  ";
        var exception = Assert.Throws<ValidationException>(() => Category.Create(name, null, CreatedAtUtc));
        Assert.Equal("El nombre de la categoría no puede superar los 100 caracteres.", exception.Message);
    }

    [Fact]
    public void BR04_Create_Accepts_NameOf100CharactersWithSurroundingSpaces()
    {
        var name = new string('a', 100);
        var category = Category.Create($"  {name}  ", null, CreatedAtUtc);
        Assert.Equal(name, category.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_StoresNullDescription_WhenDescriptionIsMissingOrWhitespace(string? description)
    {
        var category = Category.Create("Tornillería", description, CreatedAtUtc);
        Assert.Null(category.Description);
    }

    [Fact]
    public void Create_TrimsDescription_WhenDescriptionHasSurroundingSpaces()
    {
        var category = Category.Create("Tornillería", "  Tornillos y tuercas  ", CreatedAtUtc);
        Assert.Equal("Tornillos y tuercas", category.Description);
    }

    [Fact]
    public void Create_Accepts_DescriptionOf500Characters()
    {
        var description = new string('d', 500);
        var category = Category.Create("Tornillería", description, CreatedAtUtc);
        Assert.Equal(description, category.Description);
    }

    [Fact]
    public void Create_Rejects_DescriptionOf501CharactersAfterTrimming()
    {
        var description = $"  {new string('d', 501)}  ";
        var exception = Assert.Throws<ValidationException>(() => Category.Create("Tornillería", description, CreatedAtUtc));
        Assert.Equal("La descripción de la categoría no puede superar los 500 caracteres.", exception.Message);
    }

    [Fact]
    public void Create_StartsActive()
    {
        var category = Category.Create("Tornillería", null, CreatedAtUtc);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void Create_KeepsReceivedCreatedAtUtc()
    {
        var category = Category.Create("Tornillería", null, CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, category.CreatedAtUtc);
    }

    [Fact]
    public void Create_AssignsDistinctNewIds()
    {
        var first = Category.Create("Tornillería", null, CreatedAtUtc);
        var second = Category.Create("Tornillería", null, CreatedAtUtc);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }
}
