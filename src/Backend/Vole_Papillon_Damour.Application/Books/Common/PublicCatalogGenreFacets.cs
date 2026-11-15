using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Vole_Papillon_Damour.Domain.BookAggregate;

namespace Vole_Papillon_Damour.Application.Books.Common;

public static class PublicCatalogGenreFacets
{
    private const string CacheKey = "catalog:genre-facets";

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public static ValueTask<PublicCatalogGenreFacetResult[]> GetAsync(
        IQueryable<Book> catalogBooks,
        HybridCache cache,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync(
            CacheKey,
            catalogBooks,
            static async (books, token) =>
            {
                var rows = await books
                    .SelectMany(book => book.Genres)
                    .Where(bookGenre => bookGenre.Name != string.Empty)
                    .GroupBy(bookGenre => bookGenre.Name)
                    .Select(group => new PublicCatalogGenreFacetResult(group.Key, group.Count()))
                    .ToListAsync(token);

                return rows
                    .GroupBy(facet => facet.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(group => new PublicCatalogGenreFacetResult(
                        group.Key,
                        group.Sum(facet => facet.BookCount)))
                    .OrderByDescending(facet => facet.BookCount)
                    .ThenBy(facet => facet.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            },
            CacheOptions,
            cancellationToken: cancellationToken);
    }

    public static string? ResolveCanonicalName(
        string requestedName,
        IReadOnlyCollection<PublicCatalogGenreFacetResult> facets)
    {
        var normalizedRequest = Normalize(requestedName);
        return facets
            .FirstOrDefault(facet => Normalize(facet.Name) == normalizedRequest)
            ?.Name;
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
