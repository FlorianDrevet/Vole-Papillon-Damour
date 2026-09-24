using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.RareBooks.Queries.GetAdminRareBooks;

public sealed class GetAdminRareBooksQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminRareBooksQuery, ErrorOr<RareBookPageResult>>
{
    private const int MaxPageSize = 200;

    public async Task<ErrorOr<RareBookPageResult>> Handle(
        GetAdminRareBooksQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize <= 0 || query.PageSize > MaxPageSize)
        {
            return Errors.RareBook.InvalidPaging();
        }

        if (!Enum.IsDefined(query.Availability))
        {
            return Errors.RareBook.InvalidData("The rare book availability filter is not supported.");
        }

        RareBookStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status.Trim(), true, out RareBookStatus parsedStatus) ||
                !Enum.IsDefined(parsedStatus))
            {
                return Errors.RareBook.InvalidStatus();
            }

            status = parsedStatus;
        }

        if (query.MinPrice is < 0 || query.MaxPrice is < 0 ||
            query.MinPrice is not null && query.MaxPrice is not null && query.MinPrice > query.MaxPrice)
        {
            return Errors.RareBook.InvalidData("The rare book price range is not valid.");
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.RareBook.InvalidTimestamp();
        }

        var booksQuery = dbContext.RareBooks
            .AsNoTracking()
            .Include(book => book.Photos)
            .Where(book => status == null || book.Status == status)
            .Where(book => query.HasIsbn == null ||
                           query.HasIsbn == true && book.Isbn13 != null ||
                           query.HasIsbn == false && book.Isbn13 == null)
            .Where(book => query.MinPrice == null || book.Price >= query.MinPrice)
            .Where(book => query.MaxPrice == null || book.Price <= query.MaxPrice)
            .Where(book => query.Availability == RareBookAdminAvailability.All ||
                           query.Availability == RareBookAdminAvailability.Available && !book.IsSold ||
                           query.Availability == RareBookAdminAvailability.Sold && book.IsSold)
            .Where(book => !query.WithoutPhoto || !book.Photos.Any());

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search)}%";
            var hasIsbnSearch = Isbn13.TryCreate(search, out var isbnSearch);
            booksQuery = booksQuery.Where(book =>
                EF.Functions.Like(book.Title, pattern, "\\") ||
                EF.Functions.Like(book.AuthorMention ?? string.Empty, pattern, "\\") ||
                EF.Functions.Like(book.Publisher ?? string.Empty, pattern, "\\") ||
                hasIsbnSearch && book.Isbn13 == isbnSearch);
        }

        var totalCount = await booksQuery.CountAsync(cancellationToken);
        var books = await booksQuery
            .OrderByDescending(book => book.UpdatedAt)
            .ThenBy(book => book.Title)
            .ThenBy(book => book.Id)
            .Skip(checked((query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new RareBookPageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            books.Select(RareBookProjector.ToResult).ToArray(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
    }
}
