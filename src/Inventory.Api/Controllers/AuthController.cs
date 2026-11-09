using Inventory.Api.Authorization;
using Inventory.Api.Contracts;
using Inventory.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const string ClientIdClaimType = "azp";
    private const string ScopeClaimType = "scope";

    [HttpGet("whoami")]
    public ActionResult<WhoAmIResponse> WhoAmI()
    {
        var clientId = User.FindFirst(ClientIdClaimType)?.Value ?? string.Empty;
        var scopeSet = new ScopeSet(User.FindFirst(ScopeClaimType)?.Value);

        return Ok(new WhoAmIResponse(clientId, scopeSet.Scopes));
    }

    [HttpGet("check-read")]
    [Authorize(Policy = ScopePolicies.InventoryRead)]
    public ActionResult<AccessCheckResponse> CheckRead()
    {
        return Ok(new AccessCheckResponse("Permiso de lectura concedido: el token tiene el scope inventory.read."));
    }

    [HttpGet("check-write")]
    [Authorize(Policy = ScopePolicies.InventoryWrite)]
    public ActionResult<AccessCheckResponse> CheckWrite()
    {
        return Ok(new AccessCheckResponse("Permiso de escritura concedido: el token tiene el scope inventory.write."));
    }
}
