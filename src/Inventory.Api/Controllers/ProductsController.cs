using Inventory.Api.Authorization;
using Inventory.Api.Contracts;
using Inventory.Application.Common;
using Inventory.Application.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[Route(RoutePrefix)]
public sealed class ProductsController : ControllerBase
{
    private const string RoutePrefix = "api/products";
    private const string IdRoute = "{id:guid}";

    private readonly CreateProductHandler _createProductHandler;
    private readonly UpdateProductHandler _updateProductHandler;
    private readonly DeleteProductHandler _deleteProductHandler;
    private readonly GetProductByIdHandler _getProductByIdHandler;
    private readonly ListProductsHandler _listProductsHandler;

    public ProductsController(
        CreateProductHandler createProductHandler,
        UpdateProductHandler updateProductHandler,
        DeleteProductHandler deleteProductHandler,
        GetProductByIdHandler getProductByIdHandler,
        ListProductsHandler listProductsHandler)
    {
        _createProductHandler = createProductHandler;
        _updateProductHandler = updateProductHandler;
        _deleteProductHandler = deleteProductHandler;
        _getProductByIdHandler = getProductByIdHandler;
        _listProductsHandler = listProductsHandler;
    }

    [HttpPost]
    [Authorize(Policy = ScopePolicies.InventoryWrite)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _createProductHandler.HandleAsync(request.ToCommand(), cancellationToken);

        return Created($"/{RoutePrefix}/{product.Id}", product);
    }

    [HttpPut(IdRoute)]
    [Authorize(Policy = ScopePolicies.InventoryWrite)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _updateProductHandler.HandleAsync(request.ToCommand(id), cancellationToken);

        return Ok(product);
    }

    [HttpGet(IdRoute)]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _getProductByIdHandler.HandleAsync(new GetProductByIdQuery(id), cancellationToken);

        return Ok(product);
    }

    [HttpGet]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ProductDto>>> List([FromQuery] ListProductsRequest request, CancellationToken cancellationToken)
    {
        var page = await _listProductsHandler.HandleAsync(request.ToQuery(), cancellationToken);

        return Ok(page);
    }

    [HttpDelete(IdRoute)]
    [Authorize(Policy = ScopePolicies.InventoryWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _deleteProductHandler.HandleAsync(new DeleteProductCommand(id), cancellationToken);

        return NoContent();
    }
}
