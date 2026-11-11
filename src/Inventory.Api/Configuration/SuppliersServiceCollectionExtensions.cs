using Inventory.Application.Suppliers;
using Inventory.Infrastructure.Persistence.Read;
using Inventory.Infrastructure.Persistence.Write;

namespace Inventory.Api.Configuration;

public static class SuppliersServiceCollectionExtensions
{
    public static IServiceCollection AddSuppliers(this IServiceCollection services)
    {
        services.AddScoped<ISupplierWriteRepository, SupplierWriteRepository>();
        services.AddScoped<ISupplierReadRepository, SupplierReadRepository>();
        services.AddScoped<CreateSupplierHandler>();
        services.AddScoped<UpdateSupplierHandler>();
        services.AddScoped<DeactivateSupplierHandler>();
        services.AddScoped<GetSupplierByIdHandler>();
        services.AddScoped<ListSuppliersHandler>();

        return services;
    }
}
