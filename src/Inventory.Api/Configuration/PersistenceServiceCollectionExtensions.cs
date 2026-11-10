using Inventory.Application.Abstractions;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Configuration;
using Inventory.Infrastructure.Persistence.Read;
using Inventory.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Configuration;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<SqlConnectionFactory>();
        services.AddDbContext<InventoryReadDbContext>((serviceProvider, options) =>
            options.UseSqlServer(serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));

        return services;
    }
}
