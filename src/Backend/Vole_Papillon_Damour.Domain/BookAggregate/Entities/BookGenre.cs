using System.Globalization;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.BookAggregate.Entities;

public sealed class BookGenre
{
    private static readonly CompareInfo NameCompareInfo = CultureInfo.GetCultureInfo("en-US").CompareInfo;
    private const CompareOptions NameCompareOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

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

        return NameCompareInfo.Compare(left, right, NameCompareOptions) == 0;
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
