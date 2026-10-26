using System.Globalization;
using System.Text;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.BookAggregate.Entities;

public sealed class BookGenre
{
    public Isbn13 BookIsbn13 { get; private set; }
    public string Name { get; private set; } = null!;

    private BookGenre(Isbn13 bookIsbn13, string name)
    {
        BookIsbn13 = bookIsbn13;
        Name = Normalize(name);
    }

    public BookGenre()
    {
    }

    public static BookGenre Create(Isbn13 bookIsbn13, string name) => new(bookIsbn13, name);

    public static bool NamesAreEquivalent(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return StringComparer.Ordinal.Equals(CreateNameComparisonKey(left), CreateNameComparisonKey(right));
    }

    private static string CreateNameComparisonKey(string name)
    {
        // CompareInfo results differ between Windows NLS and Linux ICU. Build a stable key
        // so provider variants are deduplicated consistently before SQL's CI_AI key check.
        var normalized = name.Normalize(NormalizationForm.FormKD);
        var key = new StringBuilder(normalized.Length);
        var appendSeparator = false;

        foreach (var rune in normalized.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark or
                UnicodeCategory.Format ||
                IsTransliterationModifier(rune))
            {
                continue;
            }

            if (Rune.IsWhiteSpace(rune) || IsGenreSeparator(category) || IsApostrophe(rune))
            {
                appendSeparator = key.Length > 0;
                continue;
            }

            if (appendSeparator)
            {
                key.Append(' ');
                appendSeparator = false;
            }

            // NFKD keeps the œ ligature intact, while the SQL column's CI_AI collation
            // compares it with the expanded spelling. Keep the in-memory check aligned.
            key.Append(rune.Value is 0x0152 or 0x0153
                ? "OE"
                : Rune.ToUpperInvariant(rune).ToString());
        }

        return key.ToString();
    }

    private static bool IsGenreSeparator(UnicodeCategory category)
    {
        return category == UnicodeCategory.DashPunctuation;
    }

    private static bool IsTransliterationModifier(Rune rune)
    {
        return rune.Value is 0x02B9 or 0x02BB or 0x02BC;
    }

    private static bool IsApostrophe(Rune rune)
    {
        return rune.Value is 0x0027 or 0x2018 or 0x2019 or 0xFF07 or 0x2032;
    }

    private static string Normalize(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "A genre cannot exceed 100 characters.");
        }

        return normalized;
    }
}
