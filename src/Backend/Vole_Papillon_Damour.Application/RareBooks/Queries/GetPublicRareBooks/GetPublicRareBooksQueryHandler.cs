using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetPublicRareBooks;

public sealed class GetPublicRareBooksQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetPublicRareBooksQuery, ErrorOr<PublicRareBookPageResult>>
{
    private const int MaxPageSize = 60;

    public async Task<ErrorOr<PublicRareBookPageResult>> Handle(
        GetPublicRareBooksQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize <= 0 || query.PageSize > MaxPageSize)
        {
            return Errors.RareBook.InvalidPaging();
        }

        if (!Enum.IsDefined(query.Sort))
        {
            return Errors.RareBook.InvalidSort();
        }

        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Errors.RareBook.InvalidTimestamp();
        }

        var publishedStatus = RareBookStatus.Published;
        var booksQuery = dbContext.RareBooks
            .AsNoTracking()
            .Where(book => book.Status == publishedStatus);

        var shelf = query.Shelf?.Trim();
        if (!string.IsNullOrWhiteSpace(shelf))
        {
            RareBookShelf shelfValue;
            try
            {
                shelfValue = RareBookShelf.Create(shelf);
            }
            catch (ArgumentException exception)
            {
                return Errors.RareBook.InvalidData(exception.Message);
            }

            booksQuery = booksQuery.Where(book => book.Shelf == shelfValue);
        }

        if (!query.IncludeSold)
        {
            booksQuery = booksQuery.Where(book => !book.IsSold);
        }

        var totalCount = await booksQuery.CountAsync(cancellationToken);
        var orderedQuery = query.Sort switch
        {
            RareBookSortOrder.PriceDescending => booksQuery
                .OrderByDescending(book => book.Price)
                .ThenBy(book => book.Title)
                .ThenBy(book => book.Id),
            RareBookSortOrder.PriceAscending => booksQuery
                .OrderBy(book => book.Price)
                .ThenBy(book => book.Title)
                .ThenBy(book => book.Id),
            _ => booksQuery
                .OrderByDescending(book => book.CreatedAt)
                .ThenBy(book => book.Title)
                .ThenBy(book => book.Id)
        };

        var books = await orderedQuery
            .Include(book => book.Photos)
            .Skip(checked((query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var shelves = (await dbContext.RareBooks
                .AsNoTracking()
                .Where(book => book.Status == publishedStatus)
                .Select(book => book.Shelf)
                .ToListAsync(cancellationToken))
            .Select(value => value.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new PublicRareBookPageResult(
            new DateTimeOffset(nowUtc, TimeSpan.Zero),
            books.Select(RareBookProjector.ToPublicResult).ToArray(),
            totalCount,
            query.Page,
            query.PageSize,
            shelves);
    }
}
