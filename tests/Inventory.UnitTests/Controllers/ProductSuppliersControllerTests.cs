using System.Reflection;
using Inventory.Api.Authorization;
using Inventory.Api.Contracts;
using Inventory.Api.Controllers;
using Inventory.Application.Common;
using Inventory.Application.ProductSuppliers;
using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Inventory.UnitTests.Controllers;

public class ProductSuppliersControllerTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private readonly FakeProductSupplierWriteRepository _writeRepository = new();
    private readonly FakeProductSupplierReadRepository _readRepository = new();

    [Fact]
    public async Task BR10_Link_ReturnsCreatedWithLocation_WhenLinkIsValid()
    {
        var request = new LinkProductSupplierRequest { SupplierId = SupplierId, SupplierSku = "FA-778" };
        var result = await CreateController().Link(ProductId, request, CancellationToken.None);
        var created = Assert.IsType<CreatedResult>(result.Result);
        var link = Assert.IsType<ProductSupplierDto>(created.Value);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal($"/api/products/{ProductId}/suppliers/{SupplierId}", created.Location);
        Assert.Equal("FA-778", link.SupplierSku);
    }

    [Fact]
    public async Task GetLink_ReturnsOkWithTheVisibleLink()
    {
        var visible = CreateLink();
        _readRepository.VisibleLinks.Add(visible);
        var result = await CreateController().GetLink(ProductId, SupplierId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal(visible, ok.Value);
    }

    [Fact]
    public async Task BR18_ListProductSuppliers_ReturnsOkWithThePage()
    {
        _readRepository.ActiveProductIds.Add(ProductId);
        _readRepository.VisibleLinks.Add(CreateLink());
        var result = await CreateController().ListProductSuppliers(ProductId, new PaginationRequest(), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<ProductSupplierDto>>(ok.Value);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task BR18_ListSupplierProducts_ReturnsOkWithThePage()
    {
        _readRepository.ActiveSupplierIds.Add(SupplierId);
        _readRepository.VisibleLinks.Add(CreateLink());
        var result = await CreateController().ListSupplierProducts(SupplierId, new PaginationRequest { PageSize = 5 }, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<ProductSupplierDto>>(ok.Value);
        Assert.Single(page.Items);
        Assert.Equal(5, page.PageSize);
    }

    [Theory]
    [InlineData(nameof(ProductSuppliersController.Link), ScopePolicies.InventoryWrite)]
    [InlineData(nameof(ProductSuppliersController.GetLink), ScopePolicies.InventoryRead)]
    [InlineData(nameof(ProductSuppliersController.ListProductSuppliers), ScopePolicies.InventoryRead)]
    [InlineData(nameof(ProductSuppliersController.ListSupplierProducts), ScopePolicies.InventoryRead)]
    public void Action_RequiresTheScopePolicy(string actionName, string expectedPolicy)
    {
        var action = typeof(ProductSuppliersController).GetMethod(actionName)!;
        Assert.Equal(expectedPolicy, action.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public void Controller_RequiresAuthenticationAndUsesTheProductSuppliersRoute()
    {
        var controllerType = typeof(ProductSuppliersController);
        Assert.NotNull(controllerType.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/products/{productId:guid}/suppliers", controllerType.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Theory]
    [InlineData(nameof(ProductSuppliersController.Link), "POST", null)]
    [InlineData(nameof(ProductSuppliersController.GetLink), "GET", "{supplierId:guid}")]
    [InlineData(nameof(ProductSuppliersController.ListProductSuppliers), "GET", null)]
    [InlineData(nameof(ProductSuppliersController.ListSupplierProducts), "GET", "/api/suppliers/{supplierId:guid}/products")]
    public void Action_UsesTheExpectedMethodAndRoute(string actionName, string httpMethod, string? template)
    {
        var action = typeof(ProductSuppliersController).GetMethod(actionName)!;
        var route = action.GetCustomAttributes().OfType<HttpMethodAttribute>().Single();
        Assert.Equal(new[] { httpMethod }, route.HttpMethods);
        Assert.Equal(template, route.Template);
    }

    [Fact]
    public void Controller_HasNoActionToEditOrDeleteALink()
    {
        var httpMethods = typeof(ProductSuppliersController).GetMethods()
            .SelectMany(method => method.GetCustomAttributes().OfType<HttpMethodAttribute>())
            .SelectMany(attribute => attribute.HttpMethods);
        Assert.DoesNotContain("PUT", httpMethods);
        Assert.DoesNotContain("PATCH", httpMethods);
        Assert.DoesNotContain("DELETE", httpMethods);
    }

    private static ProductSupplierDto CreateLink()
    {
        return new ProductSupplierDto(ProductId, "Tornillo 3/8", SupplierId, "Ferretería Andina", "FA-778", FakeClock.FixedUtcNow);
    }

    private ProductSuppliersController CreateController()
    {
        return new ProductSuppliersController(
            new LinkProductSupplierHandler(_writeRepository, new FakeClock()),
            new GetProductSupplierHandler(_readRepository),
            new ListProductSuppliersHandler(_readRepository),
            new ListSupplierProductsHandler(_readRepository));
    }
}
