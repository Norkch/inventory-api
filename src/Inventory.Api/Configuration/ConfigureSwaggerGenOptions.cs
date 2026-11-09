using Inventory.Application.Security;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Inventory.Api.Configuration;

public sealed class ConfigureSwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
{
    private const string OAuth2SchemeName = "oauth2";
    private const string BearerSchemeName = "Bearer";
    private const string TokenEndpointPath = "/protocol/openid-connect/token";
    private readonly AuthOptions _authOptions;

    public ConfigureSwaggerGenOptions(IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions.Value;
    }

    public void Configure(SwaggerGenOptions options)
    {
        options.AddSecurityDefinition(OAuth2SchemeName, CreateOAuth2Scheme());
        options.AddSecurityDefinition(BearerSchemeName, CreateBearerScheme());
        options.AddSecurityRequirement(CreateSecurityRequirement());
    }

    private OpenApiSecurityScheme CreateOAuth2Scheme()
    {
        return new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "OAuth2 client_credentials contra Keycloak. Usa inventory-client y su secreto.",
            Flows = new OpenApiOAuthFlows
            {
                ClientCredentials = new OpenApiOAuthFlow
                {
                    TokenUrl = new Uri(_authOptions.Issuer.TrimEnd('/') + TokenEndpointPath),
                    Scopes = new Dictionary<string, string>
                    {
                        [InventoryScopes.Read] = "Leer el inventario",
                        [InventoryScopes.Write] = "Modificar el inventario"
                    }
                }
            }
        };
    }

    private static OpenApiSecurityScheme CreateBearerScheme()
    {
        return new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Pega un token de acceso obtenido de Keycloak (sin el prefijo Bearer)."
        };
    }

    private static OpenApiSecurityRequirement CreateSecurityRequirement()
    {
        return new OpenApiSecurityRequirement
        {
            [CreateSchemeReference(OAuth2SchemeName)] = new List<string>(),
            [CreateSchemeReference(BearerSchemeName)] = new List<string>()
        };
    }

    private static OpenApiSecurityScheme CreateSchemeReference(string schemeName)
    {
        return new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = schemeName }
        };
    }
}
