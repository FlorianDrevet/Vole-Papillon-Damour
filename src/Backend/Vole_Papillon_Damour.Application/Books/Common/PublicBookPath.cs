using System.Globalization;
using System.Text;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

/// <summary>
/// Chemin public d'une fiche livre du catalogue (« /livres/{slug}-{isbn13} »), identique
/// à celui que construit le front (`src/Catalog/src/app/shared/catalog-url.ts`).
/// </summary>
public static class PublicBookPath
{
    public static string For(Isbn13 isbn13, string? title, string? authors)
    {
        return $"/livres/{Slugify(title, authors)}-{isbn13.Value}";
    }

    private static string Slugify(string? title, string? authors)
    {
        var source = string.Join(
            ' ',
            new[] {title, authors}.Where(value => !string.IsNullOrWhiteSpace(value)));
        var decomposed = source.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSeparator = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.Length == 0 ? "livre" : builder.ToString();
    }
}
