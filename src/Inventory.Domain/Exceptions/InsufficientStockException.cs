namespace Inventory.Domain.Exceptions;

public sealed class InsufficientStockException : ConflictException
{
    public InsufficientStockException(string message)
        : base(message)
    {
    }
}
