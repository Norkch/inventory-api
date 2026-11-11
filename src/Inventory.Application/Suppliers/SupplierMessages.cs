using Inventory.Domain.Entities;

namespace Inventory.Application.Suppliers;

public static class SupplierMessages
{
    public static string NitAlreadyExists(Supplier supplier)
    {
        return $"Ya existe un proveedor con el NIT '{supplier.Nit}'.";
    }

    public static string ActiveSupplierNotFound(Guid supplierId)
    {
        return $"No existe un proveedor activo con el id '{supplierId}'.";
    }
}
