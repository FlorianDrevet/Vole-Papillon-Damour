using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminBooksQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminBooksQuery, ErrorOr<AdminBookPageResult>>
{
    private const string LikeEscapeCharacter = "\\";

    public async Task<ErrorOr<AdminBookPageResult>> Handle(
        GetAdminBooksQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize is <= 0 or > 200)
        {
            return Errors.Book.InvalidAdminPage();
        }

        BookMetadataStatus? metadataStatus = null;
        if (!string.IsNullOrWhiteSpace(query.MetadataStatus))
        {
            if (!Enum.TryParse<BookMetadataStatus>(query.MetadataStatus.Trim(), true, out var parsedStatus) ||
                !Enum.IsDefined(parsedStatus))
            {
                return Error.Validation("Book.InvalidMetadataStatus", "The metadata status is not supported.");
            }

            metadataStatus = parsedStatus;
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var booksQuery = AdminQueryPredicates.Catalogue(dbContext.Books.AsNoTracking());
        if (query.Rare is { } rare)
        {
            var rareBooks = dbContext.RareBooks.AsNoTracking();
            booksQuery = rare
                ? booksQuery.Where(book => rareBooks.Any(rareBook => rareBook.Isbn13 == book.Id))
                : booksQuery.Where(book => !rareBooks.Any(rareBook => rareBook.Isbn13 == book.Id));
        }

        if (query.Hidden is { } hidden)
        {
            booksQuery = booksQuery.Where(book => book.IsHiddenFromCatalog == hidden);
        }

        if (metadataStatus is { } requestedStatus)
        {
            booksQuery = booksQuery.Where(book => book.MetadataStatus == requestedStatus);
        }

        if (query.AvailableOnly == true)
        {
            booksQuery = booksQuery.Where(book => book.QuantityAvailable > 0);
        }

        if (query.Undated == true)
        {
            booksQuery = booksQuery.Where(book => dbContext.BookAnnouncements.Any(
                announcement => announcement.Isbn13 == book.Id &&
                                announcement.Status == BookAnnouncementStatus.Announced &&
                                announcement.AssoEventsId == null));
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Search, count and paging run in SQL; LIKE follows the column collation
            // (case-insensitive, and accent-insensitive on Title and Authors).
            var pattern = $"%{EscapeLikePattern(search)}%";
            booksQuery = booksQuery.Where(book =>
                EF.Functions.Like((string)(object)book.Id, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(book.Title ?? string.Empty, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(book.Authors ?? string.Empty, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(book.Publisher ?? string.Empty, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(book.WorkId ?? string.Empty, pattern, LikeEscapeCharacter));
        }

        var totalCount = await booksQuery.CountAsync(cancellationToken);
        var pageIsbns = await booksQuery
            .OrderByDescending(book => book.UpdatedAt)
            .ThenBy(book => book.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(book => book.Id)
            .ToListAsync(cancellationToken);
        var pageBooks = pageIsbns.Count == 0
            ? []
            : await dbContext.Books.AsNoTracking()
                .Where(book => pageIsbns.Contains(book.Id))
                .Select(book => new AdminBookQueryProjection(
                    book.Id,
                    book.WorkId,
                    book.Title,
                    book.Authors,
                    book.Publisher,
                    book.PublicationYear,
                    book.PhysicalFormat,
                    book.Language,
                    book.Genre,
                    book.MetadataStatus,
                    book.MetadataSource,
                    book.ManuallyEditedFields,
                    book.QuantityAvailable,
                    book.SalesCount,
                    book.RejectionCount,
                    book.IsHiddenFromCatalog,
                    book.RedirectedToIsbn13,
                    book.CoverUrl,
                    book.FirstSeenAt,
                    book.LastAvailableAt,
                    book.UpdatedAt,
                    book.IsIsbndbSourced,
                    book.IsAbsentFromIsbndb,
                    book.IsbndbCheckedAt,
                    book.IsbndbCompletedAt,
                    book.Pages,
                    book.Edition,
                    book.Summary,
                    book.Genres.Select(genre => genre.Name).ToArray()))
                .ToListAsync(cancellationToken);
        var booksByIsbn = pageBooks.ToDictionary(book => book.Id);
        var ids = pageIsbns.ToArray();
        var rareIsbns = ids.Length == 0
            ? []
            : (await dbContext.RareBooks
                .AsNoTracking()
                .Where(rareBook => ids.Contains(rareBook.Isbn13!.Value))
                .Select(rareBook => rareBook.Isbn13!.Value.Value)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);
        var announcements = ids.Length == 0
            ? []
            : await dbContext.BookAnnouncements
                .AsNoTracking()
                .Where(announcement => ids.Contains(announcement.Isbn13))
                .ToListAsync(cancellationToken);
        var recordSubjects = ids.Length == 0
            ? new Dictionary<BookCode, string?>()
            : await dbContext.BookIsbndbRecords.AsNoTracking()
                .Where(record => ids.Contains(record.Isbn13))
                .Select(record => new { record.Isbn13, record.Subjects })
                .ToDictionaryAsync(record => (BookCode)record.Isbn13, record => record.Subjects, cancellationToken);
        var rawPayloadIsbns = ids
            .Where(isbn => IsbndbSubjectReader.Read(recordSubjects.GetValueOrDefault(isbn), null).Count == 0)
            .ToArray();
        var rawPayloads = rawPayloadIsbns.Length == 0
            ? new Dictionary<BookCode, string?>()
            : await dbContext.Books.AsNoTracking()
                .Where(book => rawPayloadIsbns.Contains(book.Id))
                .Select(book => new { book.Id, book.RawPayload })
                .ToDictionaryAsync(book => book.Id, book => book.RawPayload, cancellationToken);

        // The page projection does not include movements, so they are not loaded.
        return new AdminBookPageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            pageIsbns
                .Select(isbn => AdminQueryProjection.ToBookResult(
                    booksByIsbn[isbn],
                    announcements,
                    rareIsbns.Contains(isbn.Value),
                    recordSubjects.GetValueOrDefault(isbn),
                    rawPayloads.GetValueOrDefault(isbn)))
                .ToArray(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
            .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal)
            .Replace("[", LikeEscapeCharacter + "[", StringComparison.Ordinal);
    }
}
