using Inventory.Domain.Entities;

namespace Inventory.Application.Categories;

public sealed record CategoryDto(Guid Id, string Name, string? Description, DateTime CreatedAtUtc)
{
    public static CategoryDto FromCategory(Category category)
    {
        return new CategoryDto(category.Id, category.Name, category.Description, category.CreatedAtUtc);
    }
}
