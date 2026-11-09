using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.UnitTests.Configuration;

public class JwtTokenValidationTests
{
    private readonly FakeTokenIssuer _tokenIssuer = new FakeTokenIssuer();

    [Fact]
    public void ValidateToken_Accepts_ValidToken()
    {
        var token = _tokenIssuer.IssueToken(FakeTokenRequest.Valid());
        var principal = ValidateToken(token, _tokenIssuer.SigningKey);
        Assert.Equal(FakePrincipals.ClientId, principal.FindFirst("azp")?.Value);
    }

    [Fact]
    public void ValidateToken_Rejects_TokenSignedWithAnotherKey()
    {
        var token = _tokenIssuer.IssueToken(FakeTokenRequest.Valid());
        var otherKey = new FakeTokenIssuer().SigningKey;
        Assert.ThrowsAny<SecurityTokenInvalidSignatureException>(() => ValidateToken(token, otherKey));
    }

    [Fact]
    public void ValidateToken_Rejects_TokenExpiredBeyondClockSkew()
    {
        var request = FakeTokenRequest.Valid() with { Expires = DateTime.UtcNow.AddSeconds(-40) };
        var token = _tokenIssuer.IssueToken(request);
        Assert.Throws<SecurityTokenExpiredException>(() => ValidateToken(token, _tokenIssuer.SigningKey));
    }

    [Fact]
    public void ValidateToken_Accepts_TokenExpiredWithinClockSkew()
    {
        var request = FakeTokenRequest.Valid() with { Expires = DateTime.UtcNow.AddSeconds(-10) };
        var token = _tokenIssuer.IssueToken(request);
        Assert.NotNull(ValidateToken(token, _tokenIssuer.SigningKey));
    }

    [Fact]
    public void ValidateToken_Rejects_TokenFromAnotherIssuer()
    {
        var request = FakeTokenRequest.Valid() with { Issuer = "http://localhost:8081/realms/other" };
        var token = _tokenIssuer.IssueToken(request);
        Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateToken(token, _tokenIssuer.SigningKey));
    }

    [Fact]
    public void ValidateToken_Rejects_TokenForAnotherAudience()
    {
        var request = FakeTokenRequest.Valid() with { Audience = "other-api" };
        var token = _tokenIssuer.IssueToken(request);
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => ValidateToken(token, _tokenIssuer.SigningKey));
    }

    private static ClaimsPrincipal ValidateToken(string token, SecurityKey publishedKey)
    {
        var options = FakeJwtBearerOptions.CreateConfigured(JwtBearerDefaults.AuthenticationScheme);
        options.TokenValidationParameters.IssuerSigningKey = publishedKey;

        return new JwtSecurityTokenHandler { MapInboundClaims = options.MapInboundClaims }.ValidateToken(token, options.TokenValidationParameters, out _);
    }
}
