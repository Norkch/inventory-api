using Inventory.Application.Products;
using Inventory.Infrastructure.Persistence.Read;
using Inventory.Infrastructure.Persistence.Write;

namespace Inventory.Api.Configuration;

public static class ProductsServiceCollectionExtensions
{
    public static IServiceCollection AddProducts(this IServiceCollection services)
    {
        services.AddScoped<IProductWriteRepository, ProductWriteRepository>();
        services.AddScoped<IProductReadRepository, ProductReadRepository>();
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<DeleteProductHandler>();
        services.AddScoped<GetProductByIdHandler>();
        services.AddScoped<ListProductsHandler>();

        return services;
    }
}
