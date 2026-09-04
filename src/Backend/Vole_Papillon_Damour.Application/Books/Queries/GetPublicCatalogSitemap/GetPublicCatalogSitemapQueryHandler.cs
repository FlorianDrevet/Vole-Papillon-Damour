using System.Globalization;
using System.Text;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetPublicCatalogSitemap;

public sealed class GetPublicCatalogSitemapQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetPublicCatalogSitemapQuery, ErrorOr<PublicCatalogSitemapResult>>
{
    public async Task<ErrorOr<PublicCatalogSitemapResult>> Handle(
        GetPublicCatalogSitemapQuery query,
        CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Catalog.InvalidClock",
                "The catalog clock must be expressed in UTC.");
        }

        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book => !book.IsHiddenFromCatalog && book.RedirectedToIsbn13 == null)
            .OrderBy(book => book.Id)
            .ToListAsync(cancellationToken);

        return new PublicCatalogSitemapResult(
            books.Select(book => new PublicCatalogSitemapEntry(
                    $"/livres/{Slugify(book.Title, book.Authors)}-{book.Id.Value}",
                    new DateTimeOffset(book.UpdatedAt, TimeSpan.Zero)))
                .ToArray());
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
