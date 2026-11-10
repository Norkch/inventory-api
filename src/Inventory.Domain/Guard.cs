using Inventory.Domain.Exceptions;

namespace Inventory.Domain;

public static class Guard
{
    public static void AgainstBlank(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message);
        }
    }

    public static void AgainstTooLong(string value, int maxLength, string message)
    {
        if (value.Length > maxLength)
        {
            throw new ValidationException(message);
        }
    }
}
