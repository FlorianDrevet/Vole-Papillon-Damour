using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.SearchRareBooksForCash;

public sealed class SearchRareBooksForCashQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SearchRareBooksForCashQuery, ErrorOr<RareBookCashSearchResult>>
{
    private const int MaxPageSize = 60;

    public async Task<ErrorOr<RareBookCashSearchResult>> Handle(
        SearchRareBooksForCashQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize <= 0 || query.PageSize > MaxPageSize)
        {
            return Errors.RareBook.InvalidPaging();
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.RareBook.InvalidTimestamp();
        }

        var booksQuery = dbContext.RareBooks
            .AsNoTracking()
            .Include(book => book.Photos)
            .Where(book => book.Status == RareBookStatus.Published && !book.IsSold);

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal)}%";
            var hasIsbnSearch = Isbn13.TryCreate(search, out var isbnSearch);
            booksQuery = booksQuery.Where(book =>
                EF.Functions.Like(book.Title, pattern) ||
                EF.Functions.Like(book.AuthorMention ?? string.Empty, pattern) ||
                hasIsbnSearch && book.Isbn13 == isbnSearch);
        }

        var totalCount = await booksQuery.CountAsync(cancellationToken);
        var books = await booksQuery
            .OrderBy(book => book.Title)
            .ThenBy(book => book.Id)
            .Skip(checked((query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new RareBookCashSearchResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            books.Select(RareBookProjector.ToCashResult).ToArray(),
            totalCount,
            query.Page,
            query.PageSize);
    }
}
