using Inventory.Application.Common;

namespace Inventory.Application.Categories;

public sealed class ListCategoriesHandler
{
    private readonly ICategoryReadRepository _categoryReadRepository;

    public ListCategoriesHandler(ICategoryReadRepository categoryReadRepository)
    {
        _categoryReadRepository = categoryReadRepository;
    }

    public Task<PagedResult<CategoryDto>> HandleAsync(ListCategoriesQuery query, CancellationToken cancellationToken)
    {
        var pageRequest = PageRequest.Create(query.Page, query.PageSize);

        return _categoryReadRepository.ListActiveAsync(pageRequest, cancellationToken);
    }
}
