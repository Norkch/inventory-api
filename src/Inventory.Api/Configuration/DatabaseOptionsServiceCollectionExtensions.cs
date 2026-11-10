using Inventory.Infrastructure.Configuration;

namespace Inventory.Api.Configuration;

public static class DatabaseOptionsServiceCollectionExtensions
{
    public static IServiceCollection AddValidatedDatabaseOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .Validate(options => options.HasConnectionString(), DatabaseOptions.MissingConnectionStringMessage)
            .ValidateOnStart();

        return services;
    }
}
