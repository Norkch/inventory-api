using Inventory.Domain.Exceptions;

namespace Inventory.Domain;

public static class ProductSupplierRules
{
    public const int SupplierSkuMaxLength = 50;

    public static Guid ValidateSupplierId(Guid? supplierId)
    {
        if (supplierId is null || supplierId == Guid.Empty)
        {
            throw new ValidationException("El proveedor es obligatorio.");
        }

        return supplierId.Value;
    }

    public static string? ValidateSupplierSku(string? supplierSku)
    {
        if (string.IsNullOrWhiteSpace(supplierSku))
        {
            return null;
        }
        var trimmedSupplierSku = supplierSku.Trim();
        Guard.AgainstTooLong(trimmedSupplierSku, SupplierSkuMaxLength, $"El código del producto en el proveedor no puede superar los {SupplierSkuMaxLength} caracteres.");

        return trimmedSupplierSku;
    }
}
