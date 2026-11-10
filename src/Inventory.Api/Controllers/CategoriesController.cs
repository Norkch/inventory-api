using Inventory.Api.Authorization;
using Inventory.Api.Contracts;
using Inventory.Application.Categories;
using Inventory.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[Route(RoutePrefix)]
public sealed class CategoriesController : ControllerBase
{
    private const string RoutePrefix = "api/categories";
    private const string IdRoute = "{id:guid}";

    private readonly CreateCategoryHandler _createCategoryHandler;
    private readonly DeleteCategoryHandler _deleteCategoryHandler;
    private readonly GetCategoryByIdHandler _getCategoryByIdHandler;
    private readonly ListCategoriesHandler _listCategoriesHandler;

    public CategoriesController(
        CreateCategoryHandler createCategoryHandler,
        DeleteCategoryHandler deleteCategoryHandler,
        GetCategoryByIdHandler getCategoryByIdHandler,
        ListCategoriesHandler listCategoriesHandler)
    {
        _createCategoryHandler = createCategoryHandler;
        _deleteCategoryHandler = deleteCategoryHandler;
        _getCategoryByIdHandler = getCategoryByIdHandler;
        _listCategoriesHandler = listCategoriesHandler;
    }

    [HttpPost]
    [Authorize(Policy = ScopePolicies.InventoryWrite)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _createCategoryHandler.HandleAsync(request.ToCommand(), cancellationToken);

        return Created($"/{RoutePrefix}/{category.Id}", category);
    }

    [HttpGet(IdRoute)]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await _getCategoryByIdHandler.HandleAsync(new GetCategoryByIdQuery(id), cancellationToken);

        return Ok(category);
    }

    [HttpGet]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<CategoryDto>>> List([FromQuery] PaginationRequest pagination, CancellationToken cancellationToken)
    {
        var page = await _listCategoriesHandler.HandleAsync(pagination.ToListCategoriesQuery(), cancellationToken);

        return Ok(page);
    }

    [HttpDelete(IdRoute)]
    [Authorize(Policy = ScopePolicies.InventoryWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _deleteCategoryHandler.HandleAsync(new DeleteCategoryCommand(id), cancellationToken);

        return NoContent();
    }
}
