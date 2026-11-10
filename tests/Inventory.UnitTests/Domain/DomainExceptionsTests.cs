using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Domain;

public sealed class DomainExceptionsTests
{
    private const string SpanishMessage = "Mensaje para el cliente.";

    [Fact]
    public void ValidationException_KeepsSpanishMessage()
    {
        var exception = new ValidationException(SpanishMessage);
        Assert.Equal(SpanishMessage, exception.Message);
    }

    [Fact]
    public void NotFoundException_KeepsSpanishMessage()
    {
        var exception = new NotFoundException(SpanishMessage);
        Assert.Equal(SpanishMessage, exception.Message);
    }

    [Fact]
    public void ConflictException_KeepsSpanishMessage()
    {
        var exception = new ConflictException(SpanishMessage);
        Assert.Equal(SpanishMessage, exception.Message);
    }

    [Fact]
    public void InsufficientStockException_IsAConflictWithSpanishMessage()
    {
        Exception exception = new InsufficientStockException(SpanishMessage);
        Assert.IsAssignableFrom<ConflictException>(exception);
        Assert.Equal(SpanishMessage, exception.Message);
    }
}
