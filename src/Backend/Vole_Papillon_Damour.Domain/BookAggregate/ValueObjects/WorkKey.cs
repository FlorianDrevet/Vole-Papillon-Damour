using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

public static class WorkKey
{
    private static readonly HashSet<string> GenericTitles = new(StringComparer.Ordinal)
    {
        "OEUVRES",
        "OEUVRES COMPLETES",
        "POEMES",
        "POESIES",
        "THEATRE",
        "ROMANS",
        "NOUVELLES",
        "CONTES",
        "CORRESPONDANCE",
        "MEMOIRES",
        "ANTHOLOGIE"
    };

    private static readonly HashSet<string> InitialArticles = new(StringComparer.Ordinal)
    {
        "LE", "LA", "LES", "L", "UN", "UNE", "DES", "DU", "DE", "D", "THE", "A", "AN"
    };

    private static readonly Regex VolumeSuffix = new(
        @"(?:^|[\s,:])(?:TOME|T\.?|VOL(?:UME)?\.?|LIVRE)\s*(?<volume>\d{1,3}|[IVX]{1,6})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, int> RomanVolumes = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["I"] = 1,
        ["II"] = 2,
        ["III"] = 3,
        ["IV"] = 4,
        ["V"] = 5,
        ["VI"] = 6,
        ["VII"] = 7,
        ["VIII"] = 8,
        ["IX"] = 9,
        ["X"] = 10,
        ["XI"] = 11,
        ["XII"] = 12,
        ["XIII"] = 13,
        ["XIV"] = 14,
        ["XV"] = 15,
        ["XVI"] = 16,
        ["XVII"] = 17,
        ["XVIII"] = 18,
        ["XIX"] = 19,
        ["XX"] = 20
    };

    public static string? Compute(string? title, string? authors)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var titleText = title.Trim();
        var volumeMatch = VolumeSuffix.Match(titleText);
        int? volume = null;
        if (volumeMatch.Success)
        {
            volume = ParseVolume(volumeMatch.Groups["volume"].Value);
            if (volume is not null)
            {
                titleText = titleText[..volumeMatch.Index];
            }
        }

        titleText = CutAtSubtitle(titleText).Trim();
        var normalizedTitle = Normalize(titleText);
        var titleWords = normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (titleWords.Length > 1 && InitialArticles.Contains(titleWords[0]))
        {
            normalizedTitle = string.Join(' ', titleWords.Skip(1));
        }

        if (normalizedTitle.Length < 4 || GenericTitles.Contains(normalizedTitle))
        {
            return null;
        }

        var authorSurname = GetPrimaryAuthorSurname(authors);
        if (authorSurname is null)
        {
            return null;
        }

        return volume is null
            ? $"{normalizedTitle}|{authorSurname}"
            : $"{normalizedTitle}|{authorSurname}#{volume.Value}";
    }

    private static string? GetPrimaryAuthorSurname(string? authors)
    {
        if (string.IsNullOrWhiteSpace(authors))
        {
            return null;
        }

        var firstAuthor = authors
            .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstAuthor))
        {
            return null;
        }

        string surname;
        var commaIndex = firstAuthor.IndexOf(',');
        if (commaIndex >= 0)
        {
            var surnamePart = firstAuthor[..commaIndex].Trim();
            var followingPart = firstAuthor[(commaIndex + 1)..].Trim();
            var surnameWordCount = CountWords(surnamePart);
            var followingWordCount = CountWords(followingPart);

            // "Hugo, Victor" is surname-first. In a joined list such as
            // "Victor Hugo, Alexandre Dumas", the first portion is a full name.
            surname = surnameWordCount == 1 || followingWordCount <= 1
                ? surnamePart
                : Normalize(surnamePart).Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        }
        else
        {
            surname = Normalize(firstAuthor).Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        }

        var normalizedSurname = Normalize(surname);
        return string.IsNullOrWhiteSpace(normalizedSurname) ? null : normalizedSurname;
    }

    private static int CountWords(string value)
    {
        return value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static int? ParseVolume(string value)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var numericVolume))
        {
            return numericVolume;
        }

        return RomanVolumes.TryGetValue(value.ToUpperInvariant(), out var romanVolume)
            ? romanVolume
            : null;
    }

    private static string CutAtSubtitle(string title)
    {
        var delimiters = new[] { " : ", " - ", " — ", "(", "/" };
        var cutAt = delimiters
            .Select(delimiter => title.IndexOf(delimiter, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(title.Length)
            .Min();

        return title[..cutAt];
    }

    private static string Normalize(string value)
    {
        var transliterated = value
            .Replace("Œ", "OE", StringComparison.Ordinal)
            .Replace("œ", "oe", StringComparison.Ordinal)
            .Replace("Æ", "AE", StringComparison.Ordinal)
            .Replace("æ", "ae", StringComparison.Ordinal)
            .Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(transliterated.Length);
        var pendingSpace = false;

        foreach (var rune in transliterated.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (Rune.IsLetterOrDigit(rune))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(Rune.ToUpperInvariant(rune).ToString());
                pendingSpace = false;
                continue;
            }

            pendingSpace = builder.Length > 0;
        }

        return builder.ToString();
    }
}
