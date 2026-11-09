using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.UnitTests.Fakes;

public sealed class FakeTokenIssuer
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);
    private readonly JwtSecurityTokenHandler _handler = new JwtSecurityTokenHandler();

    public SecurityKey SigningKey { get; } = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));

    public string IssueToken(FakeTokenRequest request)
    {
        var token = _handler.CreateJwtSecurityToken(
            issuer: request.Issuer,
            audience: request.Audience,
            subject: new ClaimsIdentity(new[] { new Claim("azp", FakePrincipals.ClientId) }),
            notBefore: request.Expires - TokenLifetime,
            expires: request.Expires,
            issuedAt: request.Expires - TokenLifetime,
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return _handler.WriteToken(token);
    }
}
