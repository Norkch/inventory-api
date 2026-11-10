using Inventory.Application.Products;

namespace Inventory.Api.Contracts;

public sealed class ListProductsRequest
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Search { get; set; }

    public ListProductsQuery ToQuery()
    {
        return new ListProductsQuery(Page, PageSize, CategoryId, Search);
    }
}
