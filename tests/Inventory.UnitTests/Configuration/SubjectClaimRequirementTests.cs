using System.Security.Claims;
using Inventory.Api.Configuration;

namespace Inventory.UnitTests.Configuration;

public class SubjectClaimRequirementTests
{
    [Fact]
    public void Accepts_TokenWithSub()
    {
        var principal = CreatePrincipal(new Claim(SubjectClaimRequirement.ClaimType, "3f2a9c1e-0000-4000-8000-000000000001"));
        Assert.True(SubjectClaimRequirement.IsSatisfiedBy(principal));
    }

    [Fact]
    public void Rejects_TokenWithoutSub()
    {
        var principal = CreatePrincipal(new Claim("azp", "inventory-client"));
        Assert.False(SubjectClaimRequirement.IsSatisfiedBy(principal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_TokenWithBlankSub(string subject)
    {
        var principal = CreatePrincipal(new Claim(SubjectClaimRequirement.ClaimType, subject));
        Assert.False(SubjectClaimRequirement.IsSatisfiedBy(principal));
    }

    private static ClaimsPrincipal CreatePrincipal(Claim claim)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[] { claim }, "Bearer"));
    }
}
