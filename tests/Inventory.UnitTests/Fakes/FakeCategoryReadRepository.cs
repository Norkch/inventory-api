using Inventory.Application.Categories;
using Inventory.Application.Common;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeCategoryReadRepository : ICategoryReadRepository
{
    public List<CategoryDto> ActiveCategories { get; } = new();

    public PageRequest? ReceivedPageRequest { get; private set; }

    public Task<CategoryDto?> FindActiveByIdAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        return Task.FromResult(ActiveCategories.FirstOrDefault(category => category.Id == categoryId));
    }

    public Task<PagedResult<CategoryDto>> ListActiveAsync(PageRequest pageRequest, CancellationToken cancellationToken)
    {
        ReceivedPageRequest = pageRequest;
        var items = ActiveCategories.Skip((pageRequest.Page - 1) * pageRequest.PageSize).Take(pageRequest.PageSize).ToList();

        return Task.FromResult(new PagedResult<CategoryDto>(items, pageRequest, ActiveCategories.Count));
    }
}
