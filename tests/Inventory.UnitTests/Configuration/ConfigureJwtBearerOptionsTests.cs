using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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

    private static JwtBearerOptions CreateConfiguredOptions(string schemeName)
    {
        return FakeJwtBearerOptions.CreateConfigured(schemeName);
    }
}
