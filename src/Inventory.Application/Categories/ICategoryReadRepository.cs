using Inventory.Application.Common;

namespace Inventory.Application.Categories;

public interface ICategoryReadRepository
{
    Task<CategoryDto?> FindActiveByIdAsync(Guid categoryId, CancellationToken cancellationToken);

    Task<PagedResult<CategoryDto>> ListActiveAsync(PageRequest pageRequest, CancellationToken cancellationToken);
}
