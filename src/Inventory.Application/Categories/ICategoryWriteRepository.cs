using Inventory.Domain.Entities;

namespace Inventory.Application.Categories;

public interface ICategoryWriteRepository
{
    Task<bool> ExistsActiveWithSameNameAsync(Category category, CancellationToken cancellationToken);

    Task AddAsync(Category category, CancellationToken cancellationToken);

    Task<CategoryDeactivationResult> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken);
}
