using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

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

        var rareBooks = await dbContext.RareBooks
            .AsNoTracking()
            .Where(book => book.Status == RareBookStatus.Published)
            .OrderBy(book => book.Id)
            .ToListAsync(cancellationToken);

        return new PublicCatalogSitemapResult(
            books.Select(book => new PublicCatalogSitemapEntry(
                    PublicBookPath.For(book.Id, book.Title, book.Authors),
                    new DateTimeOffset(book.UpdatedAt, TimeSpan.Zero)))
                .Concat(rareBooks.Select(book => new PublicCatalogSitemapEntry(
                    $"/livres-rares/{book.Id.Value:D}",
                    new DateTimeOffset(book.UpdatedAt, TimeSpan.Zero))))
                .ToArray());
    }

}
