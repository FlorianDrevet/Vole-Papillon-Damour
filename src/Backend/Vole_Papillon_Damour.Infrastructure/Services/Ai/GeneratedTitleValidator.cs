using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vole_Papillon_Damour.Infrastructure.Services.Ai;

public static partial class GeneratedTitleValidator
{
    private const int MinimumLength = 15;
    private const int MaximumLength = 70;

    [GeneratedRegex(
        "^(?:titre|voici|proposition)(?:\\s*:)?\\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VerbosePrefixRegex();

    [GeneratedRegex(
        @"\b(?:the|and|with|from|this|our|news|event)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ObviousEnglishRegex();

    public static string? NormalizeAndValidate(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Contains('\r') ||
            candidate.Contains('\n'))
        {
            return null;
        }

        var title = candidate.Trim();
        title = RemoveWrappingQuotes(title);
        title = VerbosePrefixRegex().Replace(title, string.Empty).Trim();
        title = RemoveWrappingQuotes(title);
        title = title.Trim();

        if (title.Length is < MinimumLength or > MaximumLength ||
            title.EndsWith(".", StringComparison.Ordinal) ||
            title.EndsWith("?", StringComparison.Ordinal) ||
            title.EndsWith("!", StringComparison.Ordinal) ||
            title.Contains('#') ||
            ContainsEmoji(title) ||
            ObviousEnglishRegex().IsMatch(title))
        {
            return null;
        }

        return title;
    }

    public static bool TryValidate(string? candidate, out string title)
    {
        var normalized = NormalizeAndValidate(candidate);
        if (normalized is null)
        {
            title = string.Empty;
            return false;
        }

        title = normalized;
        return true;
    }

    private static string RemoveWrappingQuotes(string value)
    {
        if (value.Length < 2)
        {
            return value;
        }

        var quotePairs = new[]
        {
            ('"', '"'),
            ('“', '”'),
            ('«', '»'),
            ('\'', '\''),
        };
        foreach (var (opening, closing) in quotePairs)
        {
            if (value[0] == opening && value[^1] == closing)
            {
                return value[1..^1].Trim();
            }
        }

        return value;
    }

    private static bool ContainsEmoji(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.OtherSymbol or UnicodeCategory.Surrogate)
            {
                return true;
            }
        }

        return false;
    }
}
