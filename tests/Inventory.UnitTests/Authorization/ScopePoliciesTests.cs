using System.Security.Claims;
using Inventory.Api.Authorization;
using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.UnitTests.Authorization;

public class ScopePoliciesTests
{
    private readonly IAuthorizationService _authorizationService = CreateAuthorizationService();

    [Fact]
    public async Task ReadPolicy_Succeeds_WhenTokenHasOnlyReadScope()
    {
        var result = await AuthorizeAsync(FakePrincipals.Authenticated("inventory.read"), ScopePolicies.InventoryRead);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task WritePolicy_Fails_WhenTokenHasOnlyReadScope()
    {
        var result = await AuthorizeAsync(FakePrincipals.Authenticated("inventory.read"), ScopePolicies.InventoryWrite);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task WritePolicy_Succeeds_WhenTokenHasReadAndWriteScopes()
    {
        var result = await AuthorizeAsync(FakePrincipals.Authenticated("inventory.read inventory.write"), ScopePolicies.InventoryWrite);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(ScopePolicies.InventoryRead)]
    [InlineData(ScopePolicies.InventoryWrite)]
    public async Task Policy_Fails_WhenUserIsNotAuthenticated(string policyName)
    {
        var result = await AuthorizeAsync(FakePrincipals.Anonymous("inventory.read inventory.write"), policyName);
        Assert.False(result.Succeeded);
    }

    private Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, string policyName)
    {
        return _authorizationService.AuthorizeAsync(user, policyName);
    }

    private static IAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddScopePolicies());

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }
}
