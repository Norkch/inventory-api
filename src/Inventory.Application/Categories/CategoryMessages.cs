using Inventory.Domain.Entities;

namespace Inventory.Application.Categories;

public static class CategoryMessages
{
    public const string CategoryHasActiveProducts = "No se puede eliminar la categoría porque tiene productos activos.";

    public static string NameAlreadyExists(Category category)
    {
        return $"Ya existe una categoría con el nombre '{category.Name}'.";
    }

    public static string ActiveCategoryNotFound(Guid categoryId)
    {
        return $"No existe una categoría activa con el id '{categoryId}'.";
    }
}
