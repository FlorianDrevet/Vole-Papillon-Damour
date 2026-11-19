using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Isbndb.Queries.GetIsbndbAbsentBooks;

public sealed class GetIsbndbAbsentBooksQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetIsbndbAbsentBooksQuery, ErrorOr<IsbndbAbsentBooksPageResult>>
{
    public async Task<ErrorOr<IsbndbAbsentBooksPageResult>> Handle(
        GetIsbndbAbsentBooksQuery query,
        CancellationToken cancellationToken)
    {
        var status = string.IsNullOrWhiteSpace(query.Status) ? "to-complete" : query.Status.Trim();
        if (status is not ("to-complete" or "completed") || query.Page < 1 || query.PageSize is < 1 or > 200)
            return Errors.Book.InvalidAdminPage();

        var books = dbContext.Books.AsNoTracking()
            .Where(book => !book.IsInternalCode &&
                book.IsAbsentFromIsbndb &&
                book.Title != null && book.Title.Trim() != string.Empty);
        books = status == "completed"
            ? books.Where(book => book.IsbndbCompletedAt != null)
            : books.Where(book => book.IsbndbCompletedAt == null);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            books = Isbn13.TryCreate(search, out var searchedIsbn)
                ? books.Where(book => book.Id == searchedIsbn ||
                    (book.Title != null && book.Title.Contains(search)) ||
                    (book.Authors != null && book.Authors.Contains(search)))
                : books.Where(book =>
                    (book.Title != null && book.Title.Contains(search)) ||
                    (book.Authors != null && book.Authors.Contains(search)));
        }

        var total = await books.CountAsync(cancellationToken);
        var pageIsbns = await books
            .OrderByDescending(book => book.QuantityAvailable > 0)
            .ThenByDescending(book => book.FirstSeenAt)
            .ThenBy(book => book.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(book => book.Id)
            .ToListAsync(cancellationToken);
        var isbn13s = pageIsbns.ToArray();
        var pageBooks = isbn13s.Length == 0
            ? []
            : await dbContext.Books.AsNoTracking()
                .Where(book => isbn13s.Contains(book.Id))
                .Select(book => new IsbndbAbsentBookProjection(
                    book.Id,
                    book.Title,
                    book.Authors,
                    book.CoverUrl,
                    book.MetadataSource,
                    book.QuantityAvailable,
                    book.Summary,
                    book.Genre,
                    book.Publisher,
                    book.PublicationYear,
                    book.Pages,
                    book.Edition,
                    book.Genres.Select(genre => genre.Name).ToArray(),
                    book.FirstSeenAt,
                    book.IsbndbCheckedAt,
                    book.IsbndbCompletedAt))
                .ToListAsync(cancellationToken);
        var pageBooksByIsbn = pageBooks.ToDictionary(book => book.Id);
        var recordSubjects = isbn13s.Length == 0
            ? new Dictionary<BookCode, string?>()
            : await dbContext.BookIsbndbRecords.AsNoTracking()
                .Where(record => isbn13s.Contains(record.Isbn13))
                .Select(record => new { record.Isbn13, record.Subjects })
                .ToDictionaryAsync(record => (BookCode)record.Isbn13, record => record.Subjects, cancellationToken);
        var fallbackPayloadIsbns = isbn13s
            .Where(isbn => IsbndbSubjectReader.Read(recordSubjects.GetValueOrDefault(isbn), null).Count == 0)
            .ToArray();
        var fallbackPayloads = fallbackPayloadIsbns.Length == 0
            ? new Dictionary<BookCode, string?>()
            : await dbContext.Books.AsNoTracking()
                .Where(book => fallbackPayloadIsbns.Contains(book.Id))
                .Select(book => new { book.Id, book.RawPayload })
                .ToDictionaryAsync(book => book.Id, book => book.RawPayload, cancellationToken);

        return new IsbndbAbsentBooksPageResult(
            pageIsbns.Select(isbn => ToResult(
                pageBooksByIsbn[isbn],
                recordSubjects.GetValueOrDefault(isbn),
                fallbackPayloads.GetValueOrDefault(isbn))).ToArray(),
            total,
            query.Page,
            query.PageSize);
    }

    private static IsbndbAbsentBookResult ToResult(
        IsbndbAbsentBookProjection book,
        string? recordSubjects,
        string? fallbackPayload)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(book.Summary)) missing.Add("Summary");
        if (string.IsNullOrWhiteSpace(book.CoverUrl)) missing.Add("CoverUrl");
        if (string.IsNullOrWhiteSpace(book.Genre) && book.Genres.Count == 0) missing.Add("Genre");
        if (string.IsNullOrWhiteSpace(book.Publisher)) missing.Add("Publisher");
        if (book.PublicationYear is null) missing.Add("PublicationYear");
        if (book.Pages is null) missing.Add("Pages");
        if (string.IsNullOrWhiteSpace(book.Title)) missing.Add("Title");
        if (string.IsNullOrWhiteSpace(book.Authors)) missing.Add("Authors");

        return new IsbndbAbsentBookResult(
            book.Id.Value,
            book.Title,
            book.Authors,
            book.CoverUrl,
            book.MetadataSource is { } source and not BookMetadataSource.Isbndb ? source.ToString() : null,
            book.QuantityAvailable,
            missing,
            book.Summary,
            book.Genre,
            book.Publisher,
            book.PublicationYear,
            book.Pages,
            book.Edition,
            book.Genres,
            IsbndbSubjectReader.Read(recordSubjects, fallbackPayload),
            new DateTimeOffset(book.FirstSeenAt, TimeSpan.Zero),
            book.IsbndbCheckedAt is { } checkedAt ? new DateTimeOffset(checkedAt, TimeSpan.Zero) : null,
            book.IsbndbCompletedAt is { } completedAt ? new DateTimeOffset(completedAt, TimeSpan.Zero) : null);
    }

    private sealed record IsbndbAbsentBookProjection(
        BookCode Id,
        string? Title,
        string? Authors,
        string? CoverUrl,
        BookMetadataSource? MetadataSource,
        int QuantityAvailable,
        string? Summary,
        string? Genre,
        string? Publisher,
        int? PublicationYear,
        int? Pages,
        string? Edition,
        IReadOnlyList<string> Genres,
        DateTime FirstSeenAt,
        DateTime? IsbndbCheckedAt,
        DateTime? IsbndbCompletedAt);

}
