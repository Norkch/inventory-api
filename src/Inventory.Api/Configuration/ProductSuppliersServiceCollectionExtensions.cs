using Inventory.Application.ProductSuppliers;
using Inventory.Infrastructure.Persistence.Read;
using Inventory.Infrastructure.Persistence.Write;

namespace Inventory.Api.Configuration;

public static class ProductSuppliersServiceCollectionExtensions
{
    public static IServiceCollection AddProductSuppliers(this IServiceCollection services)
    {
        services.AddScoped<IProductSupplierWriteRepository, ProductSupplierWriteRepository>();
        services.AddScoped<IProductSupplierReadRepository, ProductSupplierReadRepository>();
        services.AddScoped<LinkProductSupplierHandler>();
        services.AddScoped<GetProductSupplierHandler>();
        services.AddScoped<ListProductSuppliersHandler>();
        services.AddScoped<ListSupplierProductsHandler>();

        return services;
    }
}
