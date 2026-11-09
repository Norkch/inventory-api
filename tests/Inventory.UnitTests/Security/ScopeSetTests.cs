using Inventory.Application.Security;

namespace Inventory.UnitTests.Security;

public class ScopeSetTests
{
    [Fact]
    public void Contains_ReturnsTrue_WhenScopeIsAnElementOfTheClaim()
    {
        var scopeSet = new ScopeSet("profile inventory.read email");
        Assert.True(scopeSet.Contains(InventoryScopes.Read));
    }

    [Fact]
    public void Contains_ReturnsFalse_WhenScopeOnlyMatchesPartially()
    {
        var scopeSet = new ScopeSet("inventory.readonly");
        Assert.False(scopeSet.Contains(InventoryScopes.Read));
    }

    [Fact]
    public void Contains_ReturnsFalse_WhenScopeDiffersInCase()
    {
        var scopeSet = new ScopeSet("Inventory.Read");
        Assert.False(scopeSet.Contains(InventoryScopes.Read));
    }

    [Fact]
    public void Contains_ReturnsTrue_WhenClaimHasRepeatedOrSurroundingSpaces()
    {
        var scopeSet = new ScopeSet("  inventory.read   inventory.write ");
        Assert.True(scopeSet.Contains(InventoryScopes.Write));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Contains_ReturnsFalse_WhenClaimIsEmptyOrMissing(string? scopeClaim)
    {
        var scopeSet = new ScopeSet(scopeClaim);
        Assert.False(scopeSet.Contains(InventoryScopes.Read));
    }

    [Fact]
    public void Scopes_ListsEachScopeWithoutEmptyEntries_WhenClaimHasRepeatedOrSurroundingSpaces()
    {
        var scopeSet = new ScopeSet("  inventory.read   inventory.write ");
        Assert.Equal(new[] { InventoryScopes.Read, InventoryScopes.Write }, scopeSet.Scopes);
    }
}
