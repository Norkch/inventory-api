using System.Security.Claims;
using Inventory.Api.Configuration;
using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace Inventory.UnitTests.Configuration;

public class ConfigureJwtBearerOptionsTests
{
    [Fact]
    public void Configure_ReadsMetadataIssuerAndAudienceFromAuthOptions()
    {
        var options = CreateConfiguredOptions(JwtBearerDefaults.AuthenticationScheme);
        Assert.Equal(FakeAuthOptions.MetadataAddress, options.MetadataAddress);
        Assert.Equal(FakeAuthOptions.Issuer, options.TokenValidationParameters.ValidIssuer);
        Assert.Equal(FakeAuthOptions.Audience, options.TokenValidationParameters.ValidAudience);
    }

    [Fact]
    public void Configure_ValidatesIssuerAudienceLifetimeAndSigningKey()
    {
        var parameters = CreateConfiguredOptions(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateLifetime);
        Assert.True(parameters.ValidateIssuerSigningKey);
    }

    [Fact]
    public void Configure_AllowsThirtySecondsOfClockSkew()
    {
        var options = CreateConfiguredOptions(JwtBearerDefaults.AuthenticationScheme);
        Assert.Equal(TimeSpan.FromSeconds(30), options.TokenValidationParameters.ClockSkew);
    }

    [Fact]
    public void Configure_KeepsClaimNamesAsTheyComeInTheToken()
    {
        var options = CreateConfiguredOptions(JwtBearerDefaults.AuthenticationScheme);
        Assert.False(options.MapInboundClaims);
    }

    [Fact]
    public void Configure_LeavesOtherSchemesUntouched()
    {
        var options = CreateConfiguredOptions("OtherScheme");
        Assert.Null(options.MetadataAddress);
    }

    [Fact]
    public async Task Rejects_TokenWithoutSub()
    {
        var context = CreateTokenValidatedContext(FakePrincipals.Authenticated("inventory.read"));
        await context.Options.Events.OnTokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
    }

    [Fact]
    public async Task Accepts_TokenWithSub()
    {
        var principal = FakePrincipals.Authenticated("inventory.read");
        principal.Identities.Single().AddClaim(new Claim(SubjectClaimRequirement.ClaimType, "3f2a9c1e-0000-4000-8000-000000000001"));
        var context = CreateTokenValidatedContext(principal);
        await context.Options.Events.OnTokenValidated(context);
        Assert.Null(context.Result);
    }

    private static JwtBearerOptions CreateConfiguredOptions(string schemeName)
    {
        return FakeJwtBearerOptions.CreateConfigured(schemeName);
    }

    private static TokenValidatedContext CreateTokenValidatedContext(ClaimsPrincipal principal)
    {
        var options = CreateConfiguredOptions(JwtBearerDefaults.AuthenticationScheme);
        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));

        return new TokenValidatedContext(new DefaultHttpContext(), scheme, options) { Principal = principal };
    }
}
