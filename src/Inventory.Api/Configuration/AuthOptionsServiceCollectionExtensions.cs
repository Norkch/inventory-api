namespace Inventory.Api.Configuration;

public static class AuthOptionsServiceCollectionExtensions
{
    public static IServiceCollection AddValidatedAuthOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
