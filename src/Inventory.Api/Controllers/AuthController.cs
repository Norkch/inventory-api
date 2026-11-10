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
}
