using Inventory.Application.Suppliers;

namespace Inventory.Api.Contracts;

public sealed class CreateSupplierRequest
{
    public string? Name { get; set; }

    public string? Nit { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public CreateSupplierCommand ToCommand()
    {
        return new CreateSupplierCommand(Name, Nit, Email, Phone);
    }
}
