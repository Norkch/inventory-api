using Inventory.Application.Categories;

namespace Inventory.Api.Contracts;

public sealed class PaginationRequest
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public ListCategoriesQuery ToListCategoriesQuery()
    {
        return new ListCategoriesQuery(Page, PageSize);
    }
}
