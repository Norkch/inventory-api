using Inventory.Api.Configuration;

namespace Inventory.UnitTests.Fakes;

public static class FakeAuthOptions
{
    public const string MetadataAddress = "http://keycloak:8080/realms/inventory/.well-known/openid-configuration";
    public const string Issuer = "http://localhost:8081/realms/inventory";
    public const string Audience = "inventory-api";
    public const string SwaggerClientId = "inventory-client";

    public static AuthOptions Create()
    {
        return new AuthOptions { MetadataAddress = MetadataAddress, Issuer = Issuer, Audience = Audience, SwaggerClientId = SwaggerClientId };
    }
}
