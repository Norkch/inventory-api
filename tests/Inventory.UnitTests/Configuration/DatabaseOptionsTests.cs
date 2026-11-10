using Inventory.Api.Configuration;
using Inventory.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Inventory.UnitTests.Configuration;

public class DatabaseOptionsTests
{
    private const string ConnectionStringKey = "ConnectionStrings:Inventory";
    private const string ConnectionString = "Server=test-server;Database=test-db";

    [Fact]
    public void Value_FailsNamingTheEnvironmentVariable_WhenConnectionStringIsMissing()
    {
        var settings = new Dictionary<string, string>();
        var exception = Assert.Throws<OptionsValidationException>(() => ResolveDatabaseOptions(settings));
        Assert.Contains("ConnectionStrings__Inventory", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Value_FailsNamingTheEnvironmentVariable_WhenConnectionStringIsEmpty(string connectionString)
    {
        var settings = new Dictionary<string, string> { [ConnectionStringKey] = connectionString };
        var exception = Assert.Throws<OptionsValidationException>(() => ResolveDatabaseOptions(settings));
        Assert.Contains("ConnectionStrings__Inventory", exception.Message);
    }

    [Fact]
    public void Value_BindsTheConnectionString_WhenItIsProvided()
    {
        var settings = new Dictionary<string, string> { [ConnectionStringKey] = ConnectionString };
        var databaseOptions = ResolveDatabaseOptions(settings);
        Assert.Equal(ConnectionString, databaseOptions.ConnectionString);
    }

    private static DatabaseOptions ResolveDatabaseOptions(Dictionary<string, string> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var provider = new ServiceCollection().AddValidatedDatabaseOptions(configuration).BuildServiceProvider();

        return provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    }
}
