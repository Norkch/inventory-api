using Inventory.Application.Products;

namespace Inventory.Api.Contracts;

public sealed class UpdateProductRequest
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public Guid? CategoryId { get; set; }

    public UpdateProductCommand ToCommand(Guid productId)
    {
        return new UpdateProductCommand(productId, Name, Description, CategoryId);
    }
}
