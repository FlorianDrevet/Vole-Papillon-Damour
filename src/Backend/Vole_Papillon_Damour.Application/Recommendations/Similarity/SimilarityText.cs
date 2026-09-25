using System.Globalization;
using System.Text;

namespace Vole_Papillon_Damour.Application.Recommendations.Similarity;

public static class SimilarityText
{
    public const int MinSummaryLength = 80;
    public const int MaxSummaryLength = 1500;

    private static readonly string[] EditionMarkers =
    [
        "cette edition", "ce coffret", "cette nouvelle edition", "les points forts",
        "edition abregee", "edition collector", "a l occasion des",
    ];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value
            .Replace("œ", "oe", StringComparison.OrdinalIgnoreCase)
            .Replace("æ", "ae", StringComparison.OrdinalIgnoreCase)
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(lower);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    public static string? CleanSummary(string? raw, string? title)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length < MinSummaryLength || text[0] is '«' or '"' or '“' or '—' or '-')
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(title) && Normalize(text) == Normalize(title))
        {
            return null;
        }

        var head = Normalize(text.Length > 200 ? text[..200] : text);
        if (EditionMarkers.Any(head.Contains))
        {
            return null;
        }

        return text.Length > MaxSummaryLength ? text[..MaxSummaryLength] : text;
    }
}
