using Inventory.Application.Categories;
using Inventory.Application.Movements;
using Inventory.Application.ProductSuppliers;
using Inventory.Application.Suppliers;

namespace Inventory.Api.Contracts;

public sealed class PaginationRequest
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public ListCategoriesQuery ToListCategoriesQuery()
    {
        return new ListCategoriesQuery(Page, PageSize);
    }

    public ListMovementsQuery ToListMovementsQuery(Guid productId)
    {
        return new ListMovementsQuery(productId, Page, PageSize);
    }

    public ListSuppliersQuery ToListSuppliersQuery()
    {
        return new ListSuppliersQuery(Page, PageSize);
    }

    public ListProductSuppliersQuery ToListProductSuppliersQuery(Guid productId)
    {
        return new ListProductSuppliersQuery(productId, Page, PageSize);
    }

    public ListSupplierProductsQuery ToListSupplierProductsQuery(Guid supplierId)
    {
        return new ListSupplierProductsQuery(supplierId, Page, PageSize);
    }
}
