using Inventory.Application.Suppliers;

namespace Inventory.Api.Contracts;

public sealed class UpdateSupplierRequest
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public UpdateSupplierCommand ToCommand(Guid supplierId)
    {
        return new UpdateSupplierCommand(supplierId, Name, Email, Phone);
    }
}
