using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Api.Configuration;

public sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private const string MissingSubjectFailure = "The token has no 'sub' claim.";
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromSeconds(30);
    private readonly AuthOptions _authOptions;

    public ConfigureJwtBearerOptions(IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions.Value;
    }

    public void Configure(string name, JwtBearerOptions options)
    {
        if (name == JwtBearerDefaults.AuthenticationScheme)
        {
            Configure(options);
        }
    }

    public void Configure(JwtBearerOptions options)
    {
        options.MetadataAddress = _authOptions.MetadataAddress;
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = CreateTokenValidationParameters();
        options.Events = new JwtBearerEvents { OnTokenValidated = RejectTokenWithoutSubject };
    }

    private static Task RejectTokenWithoutSubject(TokenValidatedContext context)
    {
        if (context.Principal is null || !SubjectClaimRequirement.IsSatisfiedBy(context.Principal))
        {
            context.Fail(MissingSubjectFailure);
        }

        return Task.CompletedTask;
    }

    private TokenValidationParameters CreateTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _authOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _authOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = AllowedClockSkew
        };
    }
}
