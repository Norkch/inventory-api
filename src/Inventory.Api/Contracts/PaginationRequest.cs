using Inventory.Application.Categories;
using Inventory.Application.Movements;

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
}
