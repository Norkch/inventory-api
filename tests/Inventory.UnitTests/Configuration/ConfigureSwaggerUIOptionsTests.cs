using Inventory.Api.Configuration;
using Inventory.UnitTests.Fakes;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Inventory.UnitTests.Configuration;

public class ConfigureSwaggerUIOptionsTests
{
    [Fact]
    public void Configure_PrefillsClientIdFromAuthOptions()
    {
        var options = new SwaggerUIOptions();
        new ConfigureSwaggerUIOptions(Options.Create(FakeAuthOptions.Create())).Configure(options);
        Assert.Equal(FakeAuthOptions.SwaggerClientId, options.OAuthConfigObject.ClientId);
    }

    [Fact]
    public void Configure_NeverPrefillsClientSecret()
    {
        var options = new SwaggerUIOptions();
        new ConfigureSwaggerUIOptions(Options.Create(FakeAuthOptions.Create())).Configure(options);
        Assert.Null(options.OAuthConfigObject.ClientSecret);
    }
}
