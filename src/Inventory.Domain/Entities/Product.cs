namespace Inventory.Domain.Entities;

public sealed class Product
{
    private const int InitialStockQuantity = 0;

    private Product(ProductSnapshot snapshot)
    {
        Id = snapshot.Id;
        CategoryId = snapshot.CategoryId;
        Sku = snapshot.Sku;
        Name = snapshot.Name;
        Description = snapshot.Description;
        StockQuantity = snapshot.StockQuantity;
        IsActive = snapshot.IsActive;
        CreatedAtUtc = snapshot.CreatedAtUtc;
        UpdatedAtUtc = snapshot.UpdatedAtUtc;
    }

    public Guid Id { get; }

    public Guid CategoryId { get; }

    public string Sku { get; }

    public string Name { get; }

    public string? Description { get; }

    public int StockQuantity { get; }

    public bool IsActive { get; }

    public DateTime CreatedAtUtc { get; }

    public DateTime? UpdatedAtUtc { get; }

    public static Product Create(string? sku, string? name, string? description, Guid? categoryId, DateTime createdAtUtc)
    {
        var validSku = ProductRules.ValidateSku(sku);
        var validName = ProductRules.ValidateName(name);
        var validDescription = ProductRules.ValidateDescription(description);
        var validCategoryId = ProductRules.ValidateCategoryId(categoryId);
        var snapshot = new ProductSnapshot(
            Id: Guid.NewGuid(),
            CategoryId: validCategoryId,
            Sku: validSku,
            Name: validName,
            Description: validDescription,
            StockQuantity: InitialStockQuantity,
            IsActive: true,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: null);

        return new Product(snapshot);
    }

    public static Product Restore(ProductSnapshot snapshot)
    {
        return new Product(snapshot);
    }
}
