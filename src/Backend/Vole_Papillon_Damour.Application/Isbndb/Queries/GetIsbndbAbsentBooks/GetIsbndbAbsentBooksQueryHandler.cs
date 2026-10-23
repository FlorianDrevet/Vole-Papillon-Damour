using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
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
            .Where(book => book.IsAbsentFromIsbndb &&
                (status == "completed" ? book.IsbndbCompletedAt != null : book.IsbndbCompletedAt == null));
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
        var pageBooks = await books
            .OrderByDescending(book => book.QuantityAvailable > 0)
            .ThenByDescending(book => book.FirstSeenAt)
            .ThenBy(book => book.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(book => book.Genres)
            .ToListAsync(cancellationToken);
        var isbn13s = pageBooks.Select(book => book.Id).ToArray();
        var records = await dbContext.BookIsbndbRecords.AsNoTracking()
            .Where(record => isbn13s.Contains(record.Isbn13))
            .ToDictionaryAsync(record => record.Isbn13, cancellationToken);

        return new IsbndbAbsentBooksPageResult(
            pageBooks.Select(book => ToResult(book, records.GetValueOrDefault(book.Id))).ToArray(),
            total,
            query.Page,
            query.PageSize);
    }

    private static IsbndbAbsentBookResult ToResult(Book book, BookIsbndbRecord? record)
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
            book.Genres.Select(genre => genre.Name).ToArray(),
            IsbndbSubjectReader.Read(book, record?.Subjects),
            new DateTimeOffset(book.FirstSeenAt, TimeSpan.Zero),
            book.IsbndbCheckedAt is { } checkedAt ? new DateTimeOffset(checkedAt, TimeSpan.Zero) : null,
            book.IsbndbCompletedAt is { } completedAt ? new DateTimeOffset(completedAt, TimeSpan.Zero) : null);
    }

}
