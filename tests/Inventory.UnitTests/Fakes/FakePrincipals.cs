using System.Security.Claims;

namespace Inventory.UnitTests.Fakes;

public static class FakePrincipals
{
    public const string ClientId = "inventory-client";
    private const string AuthenticationType = "Bearer";

    public static ClaimsPrincipal Authenticated(string scopeClaim)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(CreateClaims(scopeClaim), AuthenticationType));
    }

    public static ClaimsPrincipal Anonymous(string scopeClaim)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(CreateClaims(scopeClaim)));
    }

    private static IEnumerable<Claim> CreateClaims(string scopeClaim)
    {
        return new[] { new Claim("azp", ClientId), new Claim("scope", scopeClaim) };
    }
}
