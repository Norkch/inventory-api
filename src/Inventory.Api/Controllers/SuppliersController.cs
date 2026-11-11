using Inventory.Api.Authorization;
using Inventory.Api.Contracts;
using Inventory.Application.Common;
using Inventory.Application.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[Route(RoutePrefix)]
public sealed class SuppliersController : ControllerBase
{
    private const string RoutePrefix = "api/suppliers";
    private const string IdRoute = "{id:guid}";

    private readonly CreateSupplierHandler _createSupplierHandler;
    private readonly UpdateSupplierHandler _updateSupplierHandler;
    private readonly DeactivateSupplierHandler _deactivateSupplierHandler;
    private readonly GetSupplierByIdHandler _getSupplierByIdHandler;
    private readonly ListSuppliersHandler _listSuppliersHandler;

    public SuppliersController(
        CreateSupplierHandler createSupplierHandler,
        UpdateSupplierHandler updateSupplierHandler,
        DeactivateSupplierHandler deactivateSupplierHandler,
        GetSupplierByIdHandler getSupplierByIdHandler,
        ListSuppliersHandler listSuppliersHandler)
    {
        _createSupplierHandler = createSupplierHandler;
        _updateSupplierHandler = updateSupplierHandler;
        _deactivateSupplierHandler = deactivateSupplierHandler;
        _getSupplierByIdHandler = getSupplierByIdHandler;
        _listSuppliersHandler = listSuppliersHandler;
    }

    [HttpPost]
    [Authorize(Policy = ScopePolicies.InventoryWrite)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await _createSupplierHandler.HandleAsync(request.ToCommand(), cancellationToken);

        return Created($"/{RoutePrefix}/{supplier.Id}", supplier);
    }

    [HttpPut(IdRoute)]
    [Authorize(Policy = ScopePolicies.InventoryWrite)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierDto>> Update(Guid id, [FromBody] UpdateSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await _updateSupplierHandler.HandleAsync(request.ToCommand(id), cancellationToken);

        return Ok(supplier);
    }

    [HttpGet(IdRoute)]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await _getSupplierByIdHandler.HandleAsync(new GetSupplierByIdQuery(id), cancellationToken);

        return Ok(supplier);
    }

    [HttpGet]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    [ProducesResponseType(typeof(PagedResult<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<SupplierDto>>> List([FromQuery] PaginationRequest request, CancellationToken cancellationToken)
    {
        var page = await _listSuppliersHandler.HandleAsync(request.ToListSuppliersQuery(), cancellationToken);

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
        await _deactivateSupplierHandler.HandleAsync(new DeactivateSupplierCommand(id), cancellationToken);

        return NoContent();
    }
}
