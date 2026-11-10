namespace Inventory.Domain.Entities;

public sealed class ProductChanges
{
    private ProductChanges(Guid productId, string name, string? description, Guid categoryId, DateTime updatedAtUtc)
    {
        ProductId = productId;
        Name = name;
        Description = description;
        CategoryId = categoryId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid ProductId { get; }

    public string Name { get; }

    public string? Description { get; }

    public Guid CategoryId { get; }

    public DateTime UpdatedAtUtc { get; }

    public static ProductChanges Create(Guid productId, string? name, string? description, Guid? categoryId, DateTime updatedAtUtc)
    {
        var validName = ProductRules.ValidateName(name);
        var validDescription = ProductRules.ValidateDescription(description);
        var validCategoryId = ProductRules.ValidateCategoryId(categoryId);

        return new ProductChanges(productId, validName, validDescription, validCategoryId, updatedAtUtc);
    }
}
