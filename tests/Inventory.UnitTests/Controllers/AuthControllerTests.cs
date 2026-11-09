using System.Reflection;
using Inventory.Api.Authorization;
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
    public void CheckRead_ConfirmsReadPermissionInSpanish()
    {
        var response = ReadOkValue<AccessCheckResponse>(CreateController("inventory.read").CheckRead().Result);
        Assert.StartsWith("Permiso de lectura concedido", response.Message);
    }

    [Fact]
    public void CheckWrite_ConfirmsWritePermissionInSpanish()
    {
        var response = ReadOkValue<AccessCheckResponse>(CreateController("inventory.write").CheckWrite().Result);
        Assert.StartsWith("Permiso de escritura concedido", response.Message);
    }

    [Fact]
    public void Controller_RequiresAuthenticatedUser()
    {
        var attribute = typeof(AuthController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Null(attribute!.Policy);
    }

    [Theory]
    [InlineData(nameof(AuthController.CheckRead), ScopePolicies.InventoryRead)]
    [InlineData(nameof(AuthController.CheckWrite), ScopePolicies.InventoryWrite)]
    public void CheckEndpoint_RequiresItsScopePolicy(string actionName, string policyName)
    {
        var attribute = typeof(AuthController).GetMethod(actionName)!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(policyName, attribute?.Policy);
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
