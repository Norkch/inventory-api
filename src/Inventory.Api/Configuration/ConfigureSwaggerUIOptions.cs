using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Inventory.Api.Configuration;

public sealed class ConfigureSwaggerUIOptions : IConfigureOptions<SwaggerUIOptions>
{
    private readonly AuthOptions _authOptions;

    public ConfigureSwaggerUIOptions(IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions.Value;
    }

    public void Configure(SwaggerUIOptions options)
    {
        options.OAuthClientId(_authOptions.SwaggerClientId);
    }
}
