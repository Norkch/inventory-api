using System.Reflection;
using Inventory.Api.Contracts;
using Inventory.Api.Controllers;
using Inventory.Application.Security;
using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.UnitTests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public void WhoAmI_ReturnsClientIdAndScopes_WhenTokenHasReadScope()
    {
        var response = ReadOkValue<WhoAmIResponse>(CreateController("inventory.read").WhoAmI().Result);
        Assert.Equal(FakePrincipals.ClientId, response.ClientId);
        Assert.Equal(new[] { InventoryScopes.Read }, response.Scopes);
    }

    [Fact]
    public void WhoAmI_ReturnsNoInventoryScopes_WhenTokenHasNone()
    {
        var response = ReadOkValue<WhoAmIResponse>(CreateController("profile email").WhoAmI().Result);
        Assert.DoesNotContain(InventoryScopes.Read, response.Scopes);
        Assert.DoesNotContain(InventoryScopes.Write, response.Scopes);
    }

    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        var attribute = typeof(AuthController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Null(attribute!.Policy);
    }

    private static AuthController CreateController(string scopeClaim)
    {
        var httpContext = new DefaultHttpContext { User = FakePrincipals.Authenticated(scopeClaim) };

        return new AuthController { ControllerContext = new ControllerContext { HttpContext = httpContext } };
    }

    private static T ReadOkValue<T>(ActionResult? result)
    {
        var okResult = Assert.IsType<OkObjectResult>(result);

        return Assert.IsType<T>(okResult.Value);
    }
}
