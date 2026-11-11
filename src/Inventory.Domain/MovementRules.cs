using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.Domain;

public static class MovementRules
{
    public const int ReasonMaxLength = 250;
    public const int RegisteredByMaxLength = 100;

    public static MovementType ValidateType(string? type)
    {
        var trimmedType = type?.Trim() ?? string.Empty;
        Guard.AgainstBlank(trimmedType, "El tipo de movimiento es obligatorio.");
        if (IsNameOf(MovementType.Entry, trimmedType))
        {
            return MovementType.Entry;
        }
        if (IsNameOf(MovementType.Exit, trimmedType))
        {
            return MovementType.Exit;
        }
        throw new ValidationException($"El tipo de movimiento debe ser '{MovementType.Entry}' o '{MovementType.Exit}'.");
    }

    public static int ValidateQuantity(int? quantity)
    {
        if (quantity is null)
        {
            throw new ValidationException("La cantidad del movimiento es obligatoria.");
        }
        if (quantity <= 0)
        {
            throw new ValidationException("La cantidad del movimiento debe ser un entero mayor que 0.");
        }

        return quantity.Value;
    }

    public static string? ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }
        var trimmedReason = reason.Trim();
        Guard.AgainstTooLong(trimmedReason, ReasonMaxLength, $"El motivo del movimiento no puede superar los {ReasonMaxLength} caracteres.");

        return trimmedReason;
    }

    public static Guid? ValidateSupplier(MovementType type, Guid? supplierId)
    {
        if (type == MovementType.Exit && supplierId is not null)
        {
            throw new ValidationException("Una salida no puede indicar proveedor.");
        }

        return supplierId;
    }

    public static string ValidateRegisteredBy(string? registeredBy)
    {
        var author = registeredBy ?? string.Empty;
        Guard.AgainstBlank(author, "El autor del movimiento es obligatorio.");
        Guard.AgainstTooLong(author, RegisteredByMaxLength, $"El autor del movimiento no puede superar los {RegisteredByMaxLength} caracteres.");

        return author;
    }

    private static bool IsNameOf(MovementType movementType, string name)
    {
        return string.Equals(movementType.ToString(), name, StringComparison.OrdinalIgnoreCase);
    }
}
