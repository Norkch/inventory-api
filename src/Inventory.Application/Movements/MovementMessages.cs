namespace Inventory.Application.Movements;

public static class MovementMessages
{
    public const string InsufficientStock = "El stock disponible del producto es insuficiente para la salida.";
    public const string StockOverflow = "La entrada supera el stock máximo permitido para el producto.";
}
