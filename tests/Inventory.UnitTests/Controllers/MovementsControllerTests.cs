using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Inventory.Api.Authorization;
using Inventory.Api.Configuration;
using Inventory.Api.Contracts;
using Inventory.Api.Controllers;
using Inventory.Application.Movements;
using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Inventory.UnitTests.Controllers;

public class MovementsControllerTests
{
    private const string Subject = "3f2a9c1e-0000-4000-8000-000000000001";
    private static readonly Guid ProductId = Guid.NewGuid();

    private readonly FakeMovementWriteRepository _writeRepository = new();
    private readonly FakeMovementReadRepository _readRepository = new();

    [Fact]
    public async Task Register_ReturnsCreatedWithoutLocation_WhenMovementIsValid()
    {
        _writeRepository.StockAfter = 10;
        var request = new RegisterMovementRequest { Type = "Entry", Quantity = 10, Reason = "Compra inicial" };
        var result = await CreateController().Register(ProductId, request, CancellationToken.None);
        var created = Assert.IsType<ObjectResult>(result.Result);
        var movement = Assert.IsType<MovementDto>(created.Value);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(ProductId, movement.ProductId);
        Assert.Equal(10, movement.StockAfter);
    }

    [Fact]
    public async Task BR16_Register_PassesTheSupplierOfAnEntry()
    {
        var supplierId = Guid.NewGuid();
        var request = new RegisterMovementRequest { Type = "Entry", Quantity = 2, SupplierId = supplierId };
        var result = await CreateController().Register(ProductId, request, CancellationToken.None);
        var movement = Assert.IsType<MovementDto>(Assert.IsType<ObjectResult>(result.Result).Value);
        Assert.Equal(supplierId, movement.SupplierId);
        Assert.Equal(supplierId, Assert.Single(_writeRepository.Received).SupplierId);
    }

    [Fact]
    public async Task Register_TakesTheAuthorFromTheSubClaim()
    {
        var request = new RegisterMovementRequest { Type = "Exit", Quantity = 1 };
        var result = await CreateController().Register(ProductId, request, CancellationToken.None);
        var movement = Assert.IsType<MovementDto>(Assert.IsType<ObjectResult>(result.Result).Value);
        Assert.Equal(Subject, movement.RegisteredBy);
        Assert.Equal(Subject, Assert.Single(_writeRepository.Received).RegisteredBy);
    }

    [Fact]
    public async Task BR18_List_ReturnsOkWithThePagedShape()
    {
        _readRepository.ActiveProductIds.Add(ProductId);
        _readRepository.Movements.Add(new MovementDto(Guid.NewGuid(), ProductId, null, "Entry", 5, 5, null, FakeClock.FixedUtcNow, Subject));
        var result = await CreateController().List(ProductId, new PaginationRequest(), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = JsonSerializer.SerializeToElement(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, body.GetProperty("totalPages").GetInt32());
        Assert.Equal(ProductId, _readRepository.ReceivedCriteria?.ProductId);
    }

    [Theory]
    [InlineData(nameof(MovementsController.Register), ScopePolicies.InventoryWrite)]
    [InlineData(nameof(MovementsController.List), ScopePolicies.InventoryRead)]
    public void Action_RequiresTheScopePolicy(string actionName, string expectedPolicy)
    {
        var action = typeof(MovementsController).GetMethod(actionName)!;
        Assert.Equal(expectedPolicy, action.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public void Controller_RequiresAuthenticationAndIsNestedUnderTheProduct()
    {
        var controllerType = typeof(MovementsController);
        Assert.NotNull(controllerType.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/products/{productId:guid}/movements", controllerType.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Fact]
    public void BR13_Controller_HasNoActionToUpdateOrDeleteMovements()
    {
        var verbs = typeof(MovementsController).GetMethods()
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(attribute => attribute.HttpMethods);
        Assert.DoesNotContain(verbs, verb => verb is "PUT" or "PATCH" or "DELETE");
    }

    [Theory]
    [InlineData("RegisteredBy")]
    [InlineData("StockAfter")]
    public void RegisterRequest_HasNoFieldsOutsideTheContract(string propertyName)
    {
        Assert.Null(typeof(RegisterMovementRequest).GetProperty(propertyName));
    }

    private MovementsController CreateController()
    {
        var controller = new MovementsController(
            new RegisterMovementHandler(_writeRepository, new FakeClock()),
            new ListMovementsHandler(_readRepository));
        var identity = new ClaimsIdentity(new[] { new Claim(SubjectClaimRequirement.ClaimType, Subject) }, "Bearer");
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };

        return controller;
    }
}
