using Inventory.Infrastructure.Persistence.Read;

namespace Inventory.UnitTests.Persistence;

public sealed class LikePatternTests
{
    [Fact]
    public void ForContains_WrapsPlainTextInWildcards()
    {
        Assert.Equal("%torn%", LikePattern.ForContains("torn"));
    }

    [Theory]
    [InlineData("50%", @"%50\%%")]
    [InlineData("A_B", @"%A\_B%")]
    [InlineData("[x]", @"%\[x]%")]
    [InlineData(@"C:\tmp", @"%C:\\tmp%")]
    public void ForContains_EscapesLikeWildcardsWithBackslash(string text, string expected)
    {
        Assert.Equal(expected, LikePattern.ForContains(text));
    }

    [Fact]
    public void EscapeCharacter_IsBackslash()
    {
        Assert.Equal(@"\", LikePattern.EscapeCharacter);
    }
}
