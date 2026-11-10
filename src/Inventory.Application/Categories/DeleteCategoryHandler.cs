using Inventory.Domain.Exceptions;

namespace Inventory.Application.Categories;

public sealed class DeleteCategoryHandler
{
    private readonly ICategoryWriteRepository _categoryWriteRepository;

    public DeleteCategoryHandler(ICategoryWriteRepository categoryWriteRepository)
    {
        _categoryWriteRepository = categoryWriteRepository;
    }

    public async Task HandleAsync(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        var result = await _categoryWriteRepository.DeactivateAsync(command.Id, cancellationToken);
        ThrowIfNotDeactivated(result, command);
    }

    private static void ThrowIfNotDeactivated(CategoryDeactivationResult result, DeleteCategoryCommand command)
    {
        switch (result)
        {
            case CategoryDeactivationResult.NotFound:
                throw new NotFoundException(CategoryMessages.ActiveCategoryNotFound(command.Id));
            case CategoryDeactivationResult.HasActiveProducts:
                throw new ConflictException(CategoryMessages.CategoryHasActiveProducts);
        }
    }
}
