using Inventory.Api.Authorization;
using Inventory.Api.Configuration;
using Inventory.Api.Contracts;
using Inventory.Application.Common;
using Inventory.Application.Movements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[Route(RoutePrefix)]
public sealed class MovementsController : ControllerBase
{
    private const string RoutePrefix = "api/products/{productId:guid}/movements";

    private readonly RegisterMovementHandler _registerMovementHandler;
    private readonly ListMovementsHandler _listMovementsHandler;

    public MovementsController(RegisterMovementHandler registerMovementHandler, ListMovementsHandler listMovementsHandler)
    {
        _registerMovementHandler = registerMovementHandler;
        _listMovementsHandler = listMovementsHandler;
    }

    [HttpPost]
    [Authorize(Policy = ScopePolicies.InventoryWrite)]
    [ProducesResponseType(typeof(MovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovementDto>> Register(Guid productId, [FromBody] RegisterMovementRequest request, CancellationToken cancellationToken)
    {
        var registrant = new MovementRegistrant(User.FindFirst(SubjectClaimRequirement.ClaimType)?.Value);
        var movement = await _registerMovementHandler.HandleAsync(request.ToCommand(productId, registrant), cancellationToken);

        return StatusCode(StatusCodes.Status201Created, movement);
    }

    [HttpGet]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    [ProducesResponseType(typeof(PagedResult<MovementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<MovementDto>>> List(Guid productId, [FromQuery] PaginationRequest request, CancellationToken cancellationToken)
    {
        var page = await _listMovementsHandler.HandleAsync(request.ToListMovementsQuery(productId), cancellationToken);

        return Ok(page);
    }
}
