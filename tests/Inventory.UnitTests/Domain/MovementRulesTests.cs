using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class MovementRulesTests
{
    [Theory]
    [InlineData("Entry", MovementType.Entry)]
    [InlineData("Exit", MovementType.Exit)]
    [InlineData("exit", MovementType.Exit)]
    [InlineData("  ENTRY  ", MovementType.Entry)]
    public void BR11_ValidateType_Accepts_EntryOrExitIgnoringCaseAndSpaces(string type, MovementType expected)
    {
        Assert.Equal(expected, MovementRules.ValidateType(type));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BR11_ValidateType_Rejects_MissingOrWhitespaceType(string? type)
    {
        var exception = Assert.Throws<ValidationException>(() => MovementRules.ValidateType(type));
        Assert.Equal("El tipo de movimiento es obligatorio.", exception.Message);
    }

    [Theory]
    [InlineData("Salida")]
    [InlineData("1")]
    [InlineData("3")]
    public void BR11_ValidateType_Rejects_UnknownOrNumericType(string type)
    {
        var exception = Assert.Throws<ValidationException>(() => MovementRules.ValidateType(type));
        Assert.Equal("El tipo de movimiento debe ser 'Entry' o 'Exit'.", exception.Message);
    }

    [Fact]
    public void BR11_ValidateQuantity_Accepts_PositiveQuantity()
    {
        Assert.Equal(1, MovementRules.ValidateQuantity(1));
    }

    [Fact]
    public void BR11_ValidateQuantity_Rejects_MissingQuantity()
    {
        var exception = Assert.Throws<ValidationException>(() => MovementRules.ValidateQuantity(null));
        Assert.Equal("La cantidad del movimiento es obligatoria.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void BR11_ValidateQuantity_Rejects_ZeroOrNegativeQuantity(int quantity)
    {
        var exception = Assert.Throws<ValidationException>(() => MovementRules.ValidateQuantity(quantity));
        Assert.Equal("La cantidad del movimiento debe ser un entero mayor que 0.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateReason_ReturnsNull_WhenReasonIsMissingOrWhitespace(string? reason)
    {
        Assert.Null(MovementRules.ValidateReason(reason));
    }

    [Fact]
    public void ValidateReason_TrimsSurroundingSpaces()
    {
        Assert.Equal("Compra mensual", MovementRules.ValidateReason("  Compra mensual  "));
    }

    [Fact]
    public void ValidateReason_Accepts_ReasonOf250CharactersAfterTrimming()
    {
        var reason = new string('A', 250);
        Assert.Equal(reason, MovementRules.ValidateReason($" {reason} "));
    }

    [Fact]
    public void ValidateReason_Rejects_ReasonOf251Characters()
    {
        var exception = Assert.Throws<ValidationException>(() => MovementRules.ValidateReason(new string('A', 251)));
        Assert.Equal("El motivo del movimiento no puede superar los 250 caracteres.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateRegisteredBy_Rejects_MissingOrWhitespaceAuthor(string? registeredBy)
    {
        var exception = Assert.Throws<ValidationException>(() => MovementRules.ValidateRegisteredBy(registeredBy));
        Assert.Equal("El autor del movimiento es obligatorio.", exception.Message);
    }

    [Fact]
    public void ValidateRegisteredBy_Accepts_AuthorOf100Characters()
    {
        var registeredBy = new string('a', 100);
        Assert.Equal(registeredBy, MovementRules.ValidateRegisteredBy(registeredBy));
    }

    [Fact]
    public void ValidateRegisteredBy_Rejects_AuthorOf101Characters()
    {
        var exception = Assert.Throws<ValidationException>(() => MovementRules.ValidateRegisteredBy(new string('a', 101)));
        Assert.Equal("El autor del movimiento no puede superar los 100 caracteres.", exception.Message);
    }
}
