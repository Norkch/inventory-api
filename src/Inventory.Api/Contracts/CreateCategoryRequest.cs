using Inventory.Application.Categories;

namespace Inventory.Api.Contracts;

public sealed class CreateCategoryRequest
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public CreateCategoryCommand ToCommand()
    {
        return new CreateCategoryCommand(Name, Description);
    }
}
