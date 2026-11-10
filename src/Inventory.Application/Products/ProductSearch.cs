namespace Inventory.Application.Products;

public static class ProductSearch
{
    public static string? Normalize(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        return search.Trim();
    }
}
