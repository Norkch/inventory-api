namespace Inventory.Application.Categories;

public sealed record CreateCategoryCommand(string? Name, string? Description);
