using Inventory.Domain.Exceptions;

namespace Inventory.Application.Categories;

public sealed class GetCategoryByIdHandler
{
    private readonly ICategoryReadRepository _categoryReadRepository;

    public GetCategoryByIdHandler(ICategoryReadRepository categoryReadRepository)
    {
        _categoryReadRepository = categoryReadRepository;
    }

    public async Task<CategoryDto> HandleAsync(GetCategoryByIdQuery query, CancellationToken cancellationToken)
    {
        var category = await _categoryReadRepository.FindActiveByIdAsync(query.Id, cancellationToken);

        return category ?? throw new NotFoundException(CategoryMessages.ActiveCategoryNotFound(query.Id));
    }
}
