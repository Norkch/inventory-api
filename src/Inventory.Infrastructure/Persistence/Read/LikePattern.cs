using System.Text;

namespace Inventory.Infrastructure.Persistence.Read;

public static class LikePattern
{
    public const string EscapeCharacter = @"\";

    private const string AnyText = "%";
    private static readonly char[] SpecialCharacters = { '\\', '%', '_', '[' };

    public static string ForContains(string text)
    {
        var pattern = new StringBuilder(AnyText);
        foreach (var character in text)
        {
            AppendEscaped(pattern, character);
        }
        pattern.Append(AnyText);

        return pattern.ToString();
    }

    private static void AppendEscaped(StringBuilder pattern, char character)
    {
        if (Array.IndexOf(SpecialCharacters, character) >= 0)
        {
            pattern.Append(EscapeCharacter);
        }
        pattern.Append(character);
    }
}
