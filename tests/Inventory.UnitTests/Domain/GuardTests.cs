using Inventory.Domain;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class GuardTests
{
    private const string SpanishMessage = "El campo no es válido.";
    private const int MaxLength = 5;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("     ")]
    public void AgainstBlank_Rejects_NullEmptyOrWhitespace(string? value)
    {
        var exception = Assert.Throws<ValidationException>(() => Guard.AgainstBlank(value, SpanishMessage));

        Assert.Equal(SpanishMessage, exception.Message);
    }

    [Fact]
    public void AgainstBlank_Accepts_Text()
    {
        var exception = Record.Exception(() => Guard.AgainstBlank("Texto", SpanishMessage));

        Assert.Null(exception);
    }

    [Fact]
    public void AgainstTooLong_Accepts_TextAtMaxLength()
    {
        var exception = Record.Exception(() => Guard.AgainstTooLong(new string('a', MaxLength), MaxLength, SpanishMessage));

        Assert.Null(exception);
    }

    [Fact]
    public void AgainstTooLong_Rejects_TextOneCharacterOverMaxLength()
    {
        var exception = Assert.Throws<ValidationException>(() => Guard.AgainstTooLong(new string('a', MaxLength + 1), MaxLength, SpanishMessage));

        Assert.Equal(SpanishMessage, exception.Message);
    }
}
