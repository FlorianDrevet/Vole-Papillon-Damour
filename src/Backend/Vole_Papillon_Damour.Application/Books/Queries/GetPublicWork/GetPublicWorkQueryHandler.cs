using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetPublicWork;

public sealed class GetPublicWorkQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetPublicWorkQuery, ErrorOr<PublicCatalogWorkResult>>
{
    public async Task<ErrorOr<PublicCatalogWorkResult>> Handle(
        GetPublicWorkQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.WorkId))
        {
            return Error.Validation(
                "Catalog.InvalidWorkId",
                "A work identifier is required.");
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Catalog.InvalidClock",
                "The catalog clock must be expressed in UTC.");
        }

        var workId = query.WorkId.Trim();
        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book =>
                !book.IsHiddenFromCatalog &&
                book.RedirectedToIsbn13 == null &&
                book.WorkId == workId)
            .ToListAsync(cancellationToken);

        if (books.Count == 0)
        {
            return Error.NotFound(
                "Catalog.WorkNotFound",
                $"Work not found: {workId}.");
        }

        var isbn13s = books.Select(book => book.Id.Value).ToHashSet(StringComparer.Ordinal);
        var announcements = (await dbContext.BookAnnouncements
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .Where(announcement => isbn13s.Contains(announcement.Isbn13.Value))
            .ToArray();
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var editions = PublicCatalogProjector
            .Project(books, announcements, fairs, nowUtc)
            .OrderByDescending(book => book.QuantityAvailable > 0)
            .ThenBy(book => book.PublicationYear)
            .ThenBy(book => book.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new PublicCatalogWorkResult(
            workId,
            books
                .Select(book => book.Title)
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .OrderBy(title => title!.Length)
                .FirstOrDefault(),
            books
                .Select(book => book.Authors)
                .Where(authors => !string.IsNullOrWhiteSpace(authors))
                .OrderBy(authors => authors!.Length)
                .FirstOrDefault(),
            editions);
    }
}
