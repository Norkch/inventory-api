using Inventory.Application.Categories;
using Inventory.Infrastructure.Persistence.Read;
using Inventory.Infrastructure.Persistence.Write;

namespace Inventory.Api.Configuration;

public static class CategoriesServiceCollectionExtensions
{
    public static IServiceCollection AddCategories(this IServiceCollection services)
    {
        services.AddScoped<ICategoryWriteRepository, CategoryWriteRepository>();
        services.AddScoped<ICategoryReadRepository, CategoryReadRepository>();
        services.AddScoped<CreateCategoryHandler>();
        services.AddScoped<DeleteCategoryHandler>();
        services.AddScoped<GetCategoryByIdHandler>();
        services.AddScoped<ListCategoriesHandler>();

        return services;
    }
}
