using System.Security.Claims;

namespace Inventory.Api.Configuration;

public static class SubjectClaimRequirement
{
    public const string ClaimType = "sub";

    public static bool IsSatisfiedBy(ClaimsPrincipal principal)
    {
        return !string.IsNullOrWhiteSpace(principal.FindFirst(ClaimType)?.Value);
    }
}
