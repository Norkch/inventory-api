using Inventory.Domain.Entities;

namespace Inventory.Application.Products;

public static class ProductMessages
{
    public const string CategoryNotActive = "La categoría indicada no existe o no está activa.";

    public static string SkuAlreadyExists(Product product)
    {
        return $"Ya existe un producto con el SKU '{product.Sku}'.";
    }

    public static string ActiveProductNotFound(Guid productId)
    {
        return $"No existe un producto activo con el id '{productId}'.";
    }
}
