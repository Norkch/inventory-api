using Inventory.Application.Abstractions;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.Categories;

public sealed class CreateCategoryHandler
{
    private readonly ICategoryWriteRepository _categoryWriteRepository;
    private readonly IClock _clock;

    public CreateCategoryHandler(ICategoryWriteRepository categoryWriteRepository, IClock clock)
    {
        _categoryWriteRepository = categoryWriteRepository;
        _clock = clock;
    }

    public async Task<CategoryDto> HandleAsync(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = Category.Create(command.Name, command.Description, _clock.UtcNow);
        await EnsureNameIsFreeAsync(category, cancellationToken);
        await _categoryWriteRepository.AddAsync(category, cancellationToken);

        return CategoryDto.FromCategory(category);
    }

    private async Task EnsureNameIsFreeAsync(Category category, CancellationToken cancellationToken)
    {
        if (await _categoryWriteRepository.ExistsActiveWithSameNameAsync(category, cancellationToken))
        {
            throw new ConflictException(CategoryMessages.NameAlreadyExists(category));
        }
    }
}
