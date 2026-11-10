using Inventory.Domain.Exceptions;

namespace Inventory.Domain;

public static class ProductRules
{
    public const int SkuMaxLength = 50;
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 1000;

    public static string ValidateSku(string? sku)
    {
        var trimmedSku = sku?.Trim() ?? string.Empty;
        Guard.AgainstBlank(trimmedSku, "El SKU del producto es obligatorio.");
        Guard.AgainstTooLong(trimmedSku, SkuMaxLength, $"El SKU del producto no puede superar los {SkuMaxLength} caracteres.");

        return trimmedSku;
    }

    public static string ValidateName(string? name)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        Guard.AgainstBlank(trimmedName, "El nombre del producto es obligatorio.");
        Guard.AgainstTooLong(trimmedName, NameMaxLength, $"El nombre del producto no puede superar los {NameMaxLength} caracteres.");

        return trimmedName;
    }

    public static string? ValidateDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }
        var trimmedDescription = description.Trim();
        Guard.AgainstTooLong(trimmedDescription, DescriptionMaxLength, $"La descripción del producto no puede superar los {DescriptionMaxLength} caracteres.");

        return trimmedDescription;
    }

    public static Guid ValidateCategoryId(Guid? categoryId)
    {
        if (categoryId is null || categoryId == Guid.Empty)
        {
            throw new ValidationException("La categoría del producto es obligatoria.");
        }

        return categoryId.Value;
    }
}
