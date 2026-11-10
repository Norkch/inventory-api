using Inventory.Application.Products;

namespace Inventory.Api.Contracts;

public sealed class CreateProductRequest
{
    public string? Sku { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public Guid? CategoryId { get; set; }

    public CreateProductCommand ToCommand()
    {
        return new CreateProductCommand(Sku, Name, Description, CategoryId);
    }
}
