using System.Reflection;
using System.Text.Json;
using Inventory.Api.Authorization;
using Inventory.Api.Contracts;
using Inventory.Api.Controllers;
using Inventory.Application.Categories;
using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Inventory.UnitTests.Controllers;

public class CategoriesControllerTests
{
    private readonly FakeCategoryWriteRepository _writeRepository = new();
    private readonly FakeCategoryReadRepository _readRepository = new();

    [Fact]
    public async Task BR04_Create_ReturnsCreatedWithLocation_WhenCategoryIsValid()
    {
        var request = new CreateCategoryRequest { Name = "Tornillería", Description = "Tornillos y tuercas" };
        var result = await CreateController().Create(request, CancellationToken.None);
        var created = Assert.IsType<CreatedResult>(result.Result);
        var category = Assert.IsType<CategoryDto>(created.Value);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal($"/api/categories/{category.Id}", created.Location);
        Assert.Equal("Tornillería", category.Name);
        Assert.Equal("Tornillos y tuercas", category.Description);
        Assert.Equal(FakeClock.FixedUtcNow, category.CreatedAtUtc);
        Assert.Single(_writeRepository.Added);
    }

    [Fact]
    public async Task GetById_ReturnsOkWithTheCategory_WhenCategoryIsActive()
    {
        var category = new CategoryDto(Guid.NewGuid(), "Tornillería", null, FakeClock.FixedUtcNow);
        _readRepository.ActiveCategories.Add(category);
        var result = await CreateController().GetById(category.Id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal(category, ok.Value);
    }

    [Fact]
    public async Task BR18_List_ReturnsOkWithThePagedShape_WhenNoParametersAreSent()
    {
        AddActiveCategories(25);
        var result = await CreateController().List(new PaginationRequest(), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = JsonSerializer.SerializeToElement(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Equal(20, body.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(25, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task BR05_Delete_ReturnsNoContent_WhenCategoryIsDeactivated()
    {
        var categoryId = Guid.NewGuid();
        var result = await CreateController().Delete(categoryId, CancellationToken.None);
        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);
        Assert.Equal(categoryId, _writeRepository.DeactivatedCategoryId);
    }

    [Theory]
    [InlineData(nameof(CategoriesController.Create), ScopePolicies.InventoryWrite)]
    [InlineData(nameof(CategoriesController.Delete), ScopePolicies.InventoryWrite)]
    [InlineData(nameof(CategoriesController.GetById), ScopePolicies.InventoryRead)]
    [InlineData(nameof(CategoriesController.List), ScopePolicies.InventoryRead)]
    public void Action_RequiresTheScopePolicy(string actionName, string expectedPolicy)
    {
        var action = typeof(CategoriesController).GetMethod(actionName)!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(expectedPolicy, authorize?.Policy);
    }

    [Fact]
    public void Controller_RequiresAuthenticationAndUsesTheCategoriesRoute()
    {
        var controllerType = typeof(CategoriesController);
        Assert.NotNull(controllerType.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/categories", controllerType.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Theory]
    [InlineData(nameof(CategoriesController.GetById))]
    [InlineData(nameof(CategoriesController.Delete))]
    public void Action_ConstrainsTheIdToAGuid(string actionName)
    {
        var action = typeof(CategoriesController).GetMethod(actionName)!;
        var route = action.GetCustomAttributes().OfType<HttpMethodAttribute>().Single();
        Assert.Equal("{id:guid}", route.Template);
    }

    private void AddActiveCategories(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            _readRepository.ActiveCategories.Add(new CategoryDto(Guid.NewGuid(), $"Categoría {index:D2}", null, FakeClock.FixedUtcNow));
        }
    }

    private CategoriesController CreateController()
    {
        var clock = new FakeClock();

        return new CategoriesController(
            new CreateCategoryHandler(_writeRepository, clock),
            new DeleteCategoryHandler(_writeRepository),
            new GetCategoryByIdHandler(_readRepository),
            new ListCategoriesHandler(_readRepository));
    }
}
