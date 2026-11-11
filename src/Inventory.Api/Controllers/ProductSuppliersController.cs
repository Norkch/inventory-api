using Inventory.Api.Authorization;
using Inventory.Api.Contracts;
using Inventory.Application.Common;
using Inventory.Application.ProductSuppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[Route(RoutePrefix)]
public sealed class ProductSuppliersController : ControllerBase
{
    private const string RoutePrefix = "api/products/{productId:guid}/suppliers";
    private const string SupplierIdRoute = "{supplierId:guid}";
    private const string SupplierProductsRoute = "/api/suppliers/{supplierId:guid}/products";
    private const string LinkLocationFormat = "/api/products/{0}/suppliers/{1}";

    private readonly LinkProductSupplierHandler _linkProductSupplierHandler;
    private readonly GetProductSupplierHandler _getProductSupplierHandler;
    private readonly ListProductSuppliersHandler _listProductSuppliersHandler;
    private readonly ListSupplierProductsHandler _listSupplierProductsHandler;

    public ProductSuppliersController(
        LinkProductSupplierHandler linkProductSupplierHandler,
        GetProductSupplierHandler getProductSupplierHandler,
        ListProductSuppliersHandler listProductSuppliersHandler,
        ListSupplierProductsHandler listSupplierProductsHandler)
    {
        _linkProductSupplierHandler = linkProductSupplierHandler;
        _getProductSupplierHandler = getProductSupplierHandler;
        _listProductSuppliersHandler = listProductSuppliersHandler;
        _listSupplierProductsHandler = listSupplierProductsHandler;
    }

    [HttpPost]
    [Authorize(Policy = ScopePolicies.InventoryWrite)]
    [ProducesResponseType(typeof(ProductSupplierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductSupplierDto>> Link(Guid productId, [FromBody] LinkProductSupplierRequest request, CancellationToken cancellationToken)
    {
        var link = await _linkProductSupplierHandler.HandleAsync(request.ToCommand(productId), cancellationToken);

        return Created(string.Format(LinkLocationFormat, link.ProductId, link.SupplierId), link);
    }

    [HttpGet(SupplierIdRoute)]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    [ProducesResponseType(typeof(ProductSupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductSupplierDto>> GetLink(Guid productId, Guid supplierId, CancellationToken cancellationToken)
    {
        var link = await _getProductSupplierHandler.HandleAsync(new GetProductSupplierQuery(productId, supplierId), cancellationToken);

        return Ok(link);
    }

    [HttpGet]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    [ProducesResponseType(typeof(PagedResult<ProductSupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ProductSupplierDto>>> ListProductSuppliers(Guid productId, [FromQuery] PaginationRequest request, CancellationToken cancellationToken)
    {
        var page = await _listProductSuppliersHandler.HandleAsync(request.ToListProductSuppliersQuery(productId), cancellationToken);

        return Ok(page);
    }

    [HttpGet(SupplierProductsRoute)]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    [ProducesResponseType(typeof(PagedResult<ProductSupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ProductSupplierDto>>> ListSupplierProducts(Guid supplierId, [FromQuery] PaginationRequest request, CancellationToken cancellationToken)
    {
        var page = await _listSupplierProductsHandler.HandleAsync(request.ToListSupplierProductsQuery(supplierId), cancellationToken);

        return Ok(page);
    }
}
