namespace Inventory.Infrastructure.Persistence.Read;

public sealed class ProductWithCategory
{
    public ProductRecord Product { get; set; } = new();

    public string CategoryName { get; set; } = string.Empty;
}
