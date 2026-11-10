using Inventory.Application.Categories;
using Inventory.Domain.Entities;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeCategoryWriteRepository : ICategoryWriteRepository
{
    public List<string> ActiveNames { get; } = new();

    public List<Category> Added { get; } = new();

    public CategoryDeactivationResult DeactivationResult { get; set; } = CategoryDeactivationResult.Deactivated;

    public Guid? DeactivatedCategoryId { get; private set; }

    public Task<bool> ExistsActiveWithSameNameAsync(Category category, CancellationToken cancellationToken)
    {
        return Task.FromResult(ActiveNames.Contains(category.Name, StringComparer.OrdinalIgnoreCase));
    }

    public Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        Added.Add(category);

        return Task.CompletedTask;
    }

    public Task<CategoryDeactivationResult> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        DeactivatedCategoryId = categoryId;

        return Task.FromResult(DeactivationResult);
    }
}
