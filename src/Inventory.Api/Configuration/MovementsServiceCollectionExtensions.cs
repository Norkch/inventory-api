using Inventory.Application.Movements;
using Inventory.Infrastructure.Persistence.Read;
using Inventory.Infrastructure.Persistence.Write;

namespace Inventory.Api.Configuration;

public static class MovementsServiceCollectionExtensions
{
    public static IServiceCollection AddMovements(this IServiceCollection services)
    {
        services.AddScoped<IMovementWriteRepository, MovementWriteRepository>();
        services.AddScoped<IMovementReadRepository, MovementReadRepository>();
        services.AddScoped<RegisterMovementHandler>();
        services.AddScoped<ListMovementsHandler>();

        return services;
    }
}
