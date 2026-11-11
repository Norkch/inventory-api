using System.Reflection;
using System.Text.Json;
using Inventory.Api.Authorization;
using Inventory.Api.Contracts;
using Inventory.Api.Controllers;
using Inventory.Application.Suppliers;
using Inventory.Domain.Entities;
using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Inventory.UnitTests.Controllers;

public class SuppliersControllerTests
{
    private readonly FakeSupplierWriteRepository _writeRepository = new();
    private readonly FakeSupplierReadRepository _readRepository = new();

    [Fact]
    public async Task BR08_Create_ReturnsCreatedWithLocation_WhenSupplierIsValid()
    {
        var request = new CreateSupplierRequest { Name = "Ferretería Andina", Nit = "900123456-7", Email = "ventas@andina.com" };
        var result = await CreateController().Create(request, CancellationToken.None);
        var created = Assert.IsType<CreatedResult>(result.Result);
        var supplier = Assert.IsType<SupplierDto>(created.Value);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal($"/api/suppliers/{supplier.Id}", created.Location);
        Assert.Equal("900123456-7", supplier.Nit);
        Assert.Single(_writeRepository.Created);
    }

    [Fact]
    public async Task Update_ReturnsOkWithTheUpdatedSupplier()
    {
        var supplierId = Guid.NewGuid();
        _writeRepository.UpdateResult = Supplier.Restore(new SupplierSnapshot(supplierId, "Andina S.A.S.", "900123456-7", null, null, true, FakeClock.FixedUtcNow, FakeClock.FixedUtcNow));
        var request = new UpdateSupplierRequest { Name = "Andina S.A.S." };
        var result = await CreateController().Update(supplierId, request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var supplier = Assert.IsType<SupplierDto>(ok.Value);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal("Andina S.A.S.", supplier.Name);
        Assert.Equal(supplierId, _writeRepository.AppliedChanges?.SupplierId);
    }

    [Fact]
    public async Task GetById_ReturnsOkWithTheSupplier_WhenSupplierIsActive()
    {
        var supplier = CreateSupplierDto("Andina");
        _readRepository.ActiveSuppliers.Add(supplier);
        var result = await CreateController().GetById(supplier.Id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal(supplier, ok.Value);
    }

    [Fact]
    public async Task BR18_List_ReturnsOkWithThePagedShape()
    {
        _readRepository.ActiveSuppliers.Add(CreateSupplierDto("Andina"));
        var request = new PaginationRequest { Page = 1, PageSize = 10 };
        var result = await CreateController().List(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = JsonSerializer.SerializeToElement(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(10, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, body.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task BR09_Delete_ReturnsNoContent_WhenSupplierIsDeactivated()
    {
        var supplierId = Guid.NewGuid();
        _writeRepository.ActiveSupplierIds.Add(supplierId);
        var result = await CreateController().Delete(supplierId, CancellationToken.None);
        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);
        Assert.Equal(supplierId, _writeRepository.AppliedDeactivation?.SupplierId);
    }

    [Theory]
    [InlineData(nameof(SuppliersController.Create), ScopePolicies.InventoryWrite)]
    [InlineData(nameof(SuppliersController.Update), ScopePolicies.InventoryWrite)]
    [InlineData(nameof(SuppliersController.Delete), ScopePolicies.InventoryWrite)]
    [InlineData(nameof(SuppliersController.GetById), ScopePolicies.InventoryRead)]
    [InlineData(nameof(SuppliersController.List), ScopePolicies.InventoryRead)]
    public void Action_RequiresTheScopePolicy(string actionName, string expectedPolicy)
    {
        var action = typeof(SuppliersController).GetMethod(actionName)!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(expectedPolicy, authorize?.Policy);
    }

    [Fact]
    public void Controller_RequiresAuthenticationAndUsesTheSuppliersRoute()
    {
        var controllerType = typeof(SuppliersController);
        Assert.NotNull(controllerType.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/suppliers", controllerType.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Theory]
    [InlineData(nameof(SuppliersController.Create), "POST", null)]
    [InlineData(nameof(SuppliersController.List), "GET", null)]
    [InlineData(nameof(SuppliersController.GetById), "GET", "{id:guid}")]
    [InlineData(nameof(SuppliersController.Update), "PUT", "{id:guid}")]
    [InlineData(nameof(SuppliersController.Delete), "DELETE", "{id:guid}")]
    public void Action_UsesTheExpectedMethodAndRoute(string actionName, string httpMethod, string? template)
    {
        var action = typeof(SuppliersController).GetMethod(actionName)!;
        var route = action.GetCustomAttributes().OfType<HttpMethodAttribute>().Single();
        Assert.Equal(new[] { httpMethod }, route.HttpMethods);
        Assert.Equal(template, route.Template);
    }

    [Fact]
    public void BR08_UpdateRequest_HasNoNit()
    {
        Assert.Null(typeof(UpdateSupplierRequest).GetProperty("Nit"));
    }

    private static SupplierDto CreateSupplierDto(string name)
    {
        return new SupplierDto(Guid.NewGuid(), name, "900123456-7", null, null, FakeClock.FixedUtcNow, null);
    }

    private SuppliersController CreateController()
    {
        var clock = new FakeClock();

        return new SuppliersController(
            new CreateSupplierHandler(_writeRepository, clock),
            new UpdateSupplierHandler(_writeRepository, clock),
            new DeactivateSupplierHandler(_writeRepository, clock),
            new GetSupplierByIdHandler(_readRepository),
            new ListSuppliersHandler(_readRepository));
    }
}
