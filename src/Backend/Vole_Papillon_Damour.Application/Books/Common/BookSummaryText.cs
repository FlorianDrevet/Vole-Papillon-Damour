using System.Net;
using System.Text.RegularExpressions;

namespace Vole_Papillon_Damour.Application.Books.Common;

public static class BookSummaryText
{
    public const int MaximumLength = 20_000;

    private static readonly Regex HtmlTags = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Whitespace = new("\\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string? Clean(string? value, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = HtmlTags.Replace(WebUtility.HtmlDecode(value), " ");
        text = Whitespace.Replace(text, " ").Trim();
        if (text.Length == 0 ||
            (!string.IsNullOrWhiteSpace(title) &&
             string.Equals(text, title.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return text.Length > MaximumLength ? text[..MaximumLength].TrimEnd() : text;
    }
}
