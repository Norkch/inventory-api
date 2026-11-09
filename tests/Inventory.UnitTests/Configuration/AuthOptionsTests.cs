using Inventory.Api.Configuration;
using Inventory.UnitTests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Inventory.UnitTests.Configuration;

public class AuthOptionsTests
{
    [Theory]
    [InlineData(nameof(AuthOptions.MetadataAddress))]
    [InlineData(nameof(AuthOptions.Issuer))]
    [InlineData(nameof(AuthOptions.Audience))]
    [InlineData(nameof(AuthOptions.SwaggerClientId))]
    public void Value_FailsNamingTheOption_WhenOptionIsMissing(string optionName)
    {
        var settings = CreateCompleteSettings();
        settings.Remove(ToConfigurationKey(optionName));
        var exception = Assert.Throws<OptionsValidationException>(() => ResolveAuthOptions(settings));
        Assert.Contains(optionName, exception.Message);
    }

    [Theory]
    [InlineData(nameof(AuthOptions.MetadataAddress))]
    [InlineData(nameof(AuthOptions.Issuer))]
    [InlineData(nameof(AuthOptions.Audience))]
    [InlineData(nameof(AuthOptions.SwaggerClientId))]
    public void Value_FailsNamingTheOption_WhenOptionIsEmpty(string optionName)
    {
        var settings = CreateCompleteSettings();
        settings[ToConfigurationKey(optionName)] = string.Empty;
        var exception = Assert.Throws<OptionsValidationException>(() => ResolveAuthOptions(settings));
        Assert.Contains(optionName, exception.Message);
    }

    [Fact]
    public void Value_BindsEverySetting_WhenConfigurationIsComplete()
    {
        var authOptions = ResolveAuthOptions(CreateCompleteSettings());
        Assert.Equal(FakeAuthOptions.MetadataAddress, authOptions.MetadataAddress);
        Assert.Equal(FakeAuthOptions.Issuer, authOptions.Issuer);
        Assert.Equal(FakeAuthOptions.Audience, authOptions.Audience);
        Assert.Equal(FakeAuthOptions.SwaggerClientId, authOptions.SwaggerClientId);
    }

    private static Dictionary<string, string> CreateCompleteSettings()
    {
        return new Dictionary<string, string>
        {
            ["Auth:MetadataAddress"] = FakeAuthOptions.MetadataAddress,
            ["Auth:Issuer"] = FakeAuthOptions.Issuer,
            ["Auth:Audience"] = FakeAuthOptions.Audience,
            ["Auth:SwaggerClientId"] = FakeAuthOptions.SwaggerClientId
        };
    }

    private static string ToConfigurationKey(string optionName)
    {
        return $"{AuthOptions.SectionName}:{optionName}";
    }

    private static AuthOptions ResolveAuthOptions(Dictionary<string, string> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var provider = new ServiceCollection().AddValidatedAuthOptions(configuration).BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AuthOptions>>().Value;
    }
}
