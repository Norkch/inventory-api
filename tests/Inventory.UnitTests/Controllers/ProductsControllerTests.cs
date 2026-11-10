using System.Reflection;
using System.Text.Json;
using Inventory.Api.Authorization;
using Inventory.Api.Contracts;
using Inventory.Api.Controllers;
using Inventory.Application.Products;
using Inventory.Domain.Entities;
using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Inventory.UnitTests.Controllers;

public class ProductsControllerTests
{
    private static readonly CategoryReference Hardware = new(Guid.NewGuid(), "Tornillería");

    private readonly FakeProductWriteRepository _writeRepository = new();
    private readonly FakeProductReadRepository _readRepository = new();

    public ProductsControllerTests()
    {
        _writeRepository.ActiveCategories.Add(Hardware);
    }

    [Fact]
    public async Task BR07_Create_ReturnsCreatedWithLocation_WhenProductIsValid()
    {
        var request = new CreateProductRequest { Sku = "TOR-001", Name = "Tornillo 3/8", CategoryId = Hardware.Id };
        var result = await CreateController().Create(request, CancellationToken.None);
        var created = Assert.IsType<CreatedResult>(result.Result);
        var product = Assert.IsType<ProductDto>(created.Value);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal($"/api/products/{product.Id}", created.Location);
        Assert.Equal("Tornillería", product.CategoryName);
        Assert.Equal(0, product.StockQuantity);
        Assert.Single(_writeRepository.Added);
    }

    [Fact]
    public async Task Update_ReturnsOkWithTheUpdatedProduct()
    {
        var productId = Guid.NewGuid();
        _writeRepository.ActiveProductIds.Add(productId);
        _writeRepository.UpdateResult = Product.Restore(new ProductSnapshot(Id: productId, CategoryId: Hardware.Id, Sku: "TOR-001", Name: "Tornillo 1/2", Description: null, StockQuantity: 4, IsActive: true, CreatedAtUtc: FakeClock.FixedUtcNow, UpdatedAtUtc: FakeClock.FixedUtcNow));
        var request = new UpdateProductRequest { Name = "Tornillo 1/2", CategoryId = Hardware.Id };
        var result = await CreateController().Update(productId, request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var product = Assert.IsType<ProductDto>(ok.Value);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal("Tornillo 1/2", product.Name);
        Assert.Equal(productId, _writeRepository.AppliedChanges?.ProductId);
    }

    [Fact]
    public async Task GetById_ReturnsOkWithTheProduct_WhenProductIsActive()
    {
        var product = CreateProductDto("Tornillo");
        _readRepository.ActiveProducts.Add(product);
        var result = await CreateController().GetById(product.Id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal(product, ok.Value);
    }

    [Fact]
    public async Task BR18_List_ReturnsOkWithThePagedShapeAndTheFilters()
    {
        _readRepository.ActiveProducts.Add(CreateProductDto("Tornillo"));
        var request = new ListProductsRequest { CategoryId = Hardware.Id, Search = " torn " };
        var result = await CreateController().List(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = JsonSerializer.SerializeToElement(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, body.GetProperty("totalPages").GetInt32());
        Assert.Equal(Hardware.Id, _readRepository.ReceivedCriteria?.CategoryId);
        Assert.Equal("torn", _readRepository.ReceivedCriteria?.Search);
    }

    [Fact]
    public async Task BR06_Delete_ReturnsNoContent_WhenProductIsDeleted()
    {
        var productId = Guid.NewGuid();
        _writeRepository.ActiveProductIds.Add(productId);
        var result = await CreateController().Delete(productId, CancellationToken.None);
        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);
        Assert.Equal(productId, _writeRepository.AppliedDeletion?.ProductId);
    }

    [Theory]
    [InlineData(nameof(ProductsController.Create), ScopePolicies.InventoryWrite)]
    [InlineData(nameof(ProductsController.Update), ScopePolicies.InventoryWrite)]
    [InlineData(nameof(ProductsController.Delete), ScopePolicies.InventoryWrite)]
    [InlineData(nameof(ProductsController.GetById), ScopePolicies.InventoryRead)]
    [InlineData(nameof(ProductsController.List), ScopePolicies.InventoryRead)]
    public void Action_RequiresTheScopePolicy(string actionName, string expectedPolicy)
    {
        var action = typeof(ProductsController).GetMethod(actionName)!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(expectedPolicy, authorize?.Policy);
    }

    [Fact]
    public void Controller_RequiresAuthenticationAndUsesTheProductsRoute()
    {
        var controllerType = typeof(ProductsController);
        Assert.NotNull(controllerType.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/products", controllerType.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Theory]
    [InlineData(nameof(ProductsController.GetById))]
    [InlineData(nameof(ProductsController.Update))]
    [InlineData(nameof(ProductsController.Delete))]
    public void Action_ConstrainsTheIdToAGuid(string actionName)
    {
        var action = typeof(ProductsController).GetMethod(actionName)!;
        var route = action.GetCustomAttributes().OfType<HttpMethodAttribute>().Single();
        Assert.Equal("{id:guid}", route.Template);
    }

    [Theory]
    [InlineData("Sku")]
    [InlineData("StockQuantity")]
    public void BR01_BR07_UpdateRequest_HasNoSkuOrStock(string propertyName)
    {
        Assert.Null(typeof(UpdateProductRequest).GetProperty(propertyName));
    }

    [Fact]
    public void BR07_CreateRequest_HasNoStock()
    {
        Assert.Null(typeof(CreateProductRequest).GetProperty("StockQuantity"));
    }

    private static ProductDto CreateProductDto(string name)
    {
        return new ProductDto(Guid.NewGuid(), "TOR-001", name, null, Hardware.Id, Hardware.Name, 0, FakeClock.FixedUtcNow, null);
    }

    private ProductsController CreateController()
    {
        var clock = new FakeClock();

        return new ProductsController(
            new CreateProductHandler(_writeRepository, clock),
            new UpdateProductHandler(_writeRepository, clock),
            new DeleteProductHandler(_writeRepository, clock),
            new GetProductByIdHandler(_readRepository),
            new ListProductsHandler(_readRepository));
    }
}
