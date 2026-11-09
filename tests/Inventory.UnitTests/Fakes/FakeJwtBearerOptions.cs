using Inventory.Api.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Inventory.UnitTests.Fakes;

public static class FakeJwtBearerOptions
{
    public static JwtBearerOptions CreateConfigured(string schemeName)
    {
        var options = new JwtBearerOptions();
        new ConfigureJwtBearerOptions(Options.Create(FakeAuthOptions.Create())).Configure(schemeName, options);

        return options;
    }
}
